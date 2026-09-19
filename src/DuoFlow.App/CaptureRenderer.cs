using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using DuoFlow.Capture;
using DuoFlow.Capture.Interop;
using DuoFlow.Render;
using DuoFlow.Core;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using WinRT;

namespace DuoFlow.App;

/// <summary>
/// M0.2 display path: copies every captured GPU texture into a composition
/// swapchain bound to a SwapChainPanel. Zero CPU staging — the desktop
/// texture is blitted and presented entirely on the GPU.
///
/// M1.4 (DD-039): when a warp pipeline can be created, each frame is instead
/// drawn through the perspective warp (shaders/DuoWarp.hlsl) driven by the
/// M1.3 AnimationEngine's smoothed progress - GPU → GPU → GPU, no CPU
/// staging. The engine is Tick()-ed inside the frame callback, making the
/// capture callback the render clock (frames stop when the screen is static,
/// which is exactly when there is nothing to animate). If the warp pipeline
/// cannot be created (e.g. shader compile failure), the M0.2 straight blit
/// stays as the fallback so the CI smoke keeps passing.
///
/// The swapchain is created at the CAPTURED monitor size; the SwapChainPanel
/// stretches it to the window, so aspect ratio may not be preserved in the
/// demo (acceptable for M0.2; the real renderer arrives in M1).
/// </summary>
public sealed class CaptureRenderer : IDisposable
{
    private readonly SwapChainPanel _panel;
    private readonly DesktopCapture _capture;
    private readonly LidAnimationClock? _clock;

    private IDXGIFactory2? _factory;
    private IDXGISwapChain1? _swapChain;
    private ID3D11Texture2D? _backBuffer;
    private ID3D11DeviceContext? _context;
    private WarpPipeline? _warp;
    private readonly WarpOptions _warpOptions = new();
    private readonly HingeMaskProfile _maskProfile = HingeMask.Build();
    private readonly BlurOptions _blurOptions = new();
    private double _maxBlurNormalized;
    private int _panelPixelWidth;
    private int _panelPixelHeight;
    private bool _disposed;

    /// <summary>Total frames presented (thread-safe counter for FPS display).</summary>
    public long PresentedFrames => Interlocked.Read(ref _presented);
    private long _presented;

    /// <summary>M1.4 warp pipeline state, surfaced to the console/probe
    /// (informational; the real-machine grid check is the visual authority).</summary>
    public string WarpState { get; private set; } = "starting";

    /// <summary>M1.4 self-test support: request a one-shot staging readback of
    /// the next rendered frame into a raw BGRA file. Executed on the render
    /// thread right after the draw (before Present), so the immediate context
    /// is never touched from two threads. Used by --warp-selftest to verify
    /// the warp output IN-PROCESS - independent of the desktop composition
    /// path, which currently has a machine-level issue (see HANDOFF).</summary>
    private sealed class DumpRequest
    {
        public string Path { get; init; } = "";
        public bool DebugMask { get; init; }
    }

    private DumpRequest? _pendingDump;

    public void RequestDump(string filePath, bool debugMask = false)
        => Interlocked.Exchange(ref _pendingDump, new DumpRequest { Path = filePath, DebugMask = debugMask });

    public CaptureRenderer(SwapChainPanel panel, DesktopCapture capture, LidAnimationClock? clock = null)
    {
        _panel = panel;
        _capture = capture;
        _clock = clock;
    }

    public void Initialize(int panelPixelWidth = 0, int panelPixelHeight = 0)
    {
        _panelPixelWidth = panelPixelWidth;
        _panelPixelHeight = panelPixelHeight;

        ID3D11Device device = _capture.Device;
        _context = device.ImmediateContext;

        SizeInt32 size = _capture.Item.Size;

        // Composition swapchain sized to the PANEL'S PIXEL SIZE (2026-09-19,
        // real-machine regression workaround - see HC §6.2): after a Windows
        // update on this machine (KB5129195 active from 2026-09-19, DPI 125%),
        // a composition swapchain LARGER than the panel is no longer scaled
        // down to fit (shown 1:1, cropped) and its premultiplied alpha is not
        // composited (content invisible). Sizing the swapchain to the panel's
        // physical pixel size removes the scaling requirement entirely: the
        // panel displays it 1:1, and the warp shader is resolution-independent
        // (normalized hinge-frame coordinates), so the captured 1920x1080
        // texture renders identically into any back-buffer size.
        SizeInt32 swapSize = size;
        if (_panelPixelWidth > 0 && _panelPixelHeight > 0)
        {
            swapSize = new SizeInt32(_panelPixelWidth, _panelPixelHeight);
        }
        _factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>().QueryInterface<IDXGIFactory2>();
        var description = new SwapChainDescription1
        {
            Width = (uint)swapSize.Width,
            Height = (uint)swapSize.Height,
            Format = Format.B8G8R8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 2,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential,
            AlphaMode = AlphaMode.Premultiplied,
        };
        _swapChain = _factory.CreateSwapChainForComposition(device, description, null);

        // Bind to the WinUI SwapChainPanel via the WinUI 3 interop wrapper
        // (QI 'ISwapChainPanelNative' {63aad0b8-...} from microsoft.ui.xaml dxinterop).
        using var panelNative = new Vortice.WinUI.ISwapChainPanelNative(_panel);
        panelNative.SetSwapChain(_swapChain);

        _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);

        // M1.4: warp pipeline on the capture device (same device => the
        // captured texture can be sampled directly). Falls back to the M0.2
        // blit when it cannot be created; TryCreate traces the reason.
        _warp = WarpPipeline.TryCreate(device, _backBuffer);
        if (_warp is null && swapSize.Width != size.Width)
        {
            // Shader path unavailable: restore the M0.2 arrangement - the
            // blit fallback uses CopyResource, which requires the back buffer
            // and the captured texture to be the SAME size, so the swapchain
            // moves back to the capture size.
            Trace.Log("capture renderer: recreating swapchain at capture size for the blit fallback");
            _backBuffer.Dispose();
            _swapChain.Dispose();
            var fallbackDescription = description with { Width = (uint)size.Width, Height = (uint)size.Height };
            _swapChain = _factory.CreateSwapChainForComposition(device, fallbackDescription, null);
            using var panelNative2 = new Vortice.WinUI.ISwapChainPanelNative(_panel);
            panelNative2.SetSwapChain(_swapChain);
            _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        }
        WarpState = _warp is null ? "blit fallback (warp init failed)" : "active";
        Trace.Log($"capture renderer: warp state = {WarpState} (swapchain {swapSize.Width}×{swapSize.Height})");

        // M1.6: blur radius knob, normalized to the source frame height once
        // (the capture frame pool only changes size via RecreateFramePool,
        // which re-runs this method's caller flow - a restart for now).
        _maxBlurNormalized = _blurOptions.MaxBlurNormalized(size.Height);

        // GPU -> GPU: every captured frame goes straight to the panel.
        _capture.FrameArrived += OnFrameArrived;
    }

    private void OnFrameArrived(ID3D11Texture2D texture, SizeInt32 contentSize)
    {
        if (_disposed || _backBuffer is null || _context is null)
        {
            return;
        }

        // Consume a pending self-test dump request BEFORE drawing: the debug
        // flag must reach the constant buffer of the very frame that gets
        // dumped (M1.5 mask debug visualizations).
        DumpRequest? request = Interlocked.Exchange(ref _pendingDump, null);

        if (_warp is { } warp && _clock is { } clock)
        {
            // M1.4/M1.5: engine Tick -> smoothed progress -> warp + hinge-mask
            // constants -> draw. The AnimationEngine is the ONLY progress
            // source for the render side (DD-002/DD-038); the clock
            // serializes the UI thread's Update() against this worker
            // thread's Tick().
            LidState state = clock.Tick();
            WarpFrame frame = WarpGeometry.Compute(state.Progress, _warpOptions);
            warp.Render(_context, texture, frame, _maskProfile, request?.DebugMask ?? false, _maxBlurNormalized, state.Progress);
        }
        else
        {
            // M0.2 fallback: same-size blit on the GPU. ContentSize can lag
            // the pool size for one frame after a resolution change; clip
            // defensively.
            _context.CopyResource(_backBuffer, texture);
        }

        // Self-test dump (M1.4/M1.5): read back the just-drawn frame before it
        // is presented. Executed here on the render thread by design.
        if (request is not null)
        {
            DumpBackBuffer(request.Path);
        }

        _swapChain!.Present(1, 0);
        Interlocked.Increment(ref _presented);
    }

    /// <summary>Staging readback of the whole back buffer into a raw BGRA
    /// file (width×height×4 bytes, bottom-up row order reversed to top-down).
    /// Runs on the render thread only.</summary>
    private void DumpBackBuffer(string path)
    {
        try
        {
            if (_backBuffer is null || _context is null)
            {
                return;
            }
            Texture2DDescription desc = _backBuffer.Description;
            int width = (int)desc.Width;
            int height = (int)desc.Height;
            var stagingDesc = new Texture2DDescription
            {
                Width = desc.Width,
                Height = desc.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = desc.Format,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read,
                MiscFlags = ResourceOptionFlags.None,
            };
            using ID3D11Texture2D staging = _capture.Device.CreateTexture2D(stagingDesc);
            _context.CopyResource(staging, _backBuffer);
            MappedSubresource map = _context.Map(staging, 0, MapMode.Read);
            try
            {
                int rowBytes = width * 4;
                byte[] data = new byte[height * rowBytes];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(map.DataPointer + (int)(y * (long)map.RowPitch), data, y * rowBytes, rowBytes);
                }
                File.WriteAllBytes(path, data);
                Trace.Log($"selftest: dumped {path} ({width}×{height})");
            }
            finally
            {
                _context.Unmap(staging, 0);
            }
        }
        catch (Exception ex)
        {
            Trace.Log($"selftest: dump FAILED ({path}): {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _capture.FrameArrived -= OnFrameArrived;
        try { _warp?.Dispose(); } catch { }
        try { _backBuffer?.Dispose(); } catch { }
        try { _swapChain?.Dispose(); } catch { }
        try { _factory?.Dispose(); } catch { }
        _warp = null;
        _backBuffer = null;
        _swapChain = null;
        _factory = null;
        _context = null; // owned by the device
    }
}
