using System;
using System.Threading;
using DuoFlow.Capture;
using DuoFlow.Capture.Interop;
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
/// The swapchain is created at the CAPTURED monitor size; the SwapChainPanel
/// stretches it to the window, so aspect ratio may not be preserved in the
/// demo (acceptable for M0.2; the real renderer arrives in M1).
/// </summary>
public sealed class CaptureRenderer : IDisposable
{
    private readonly SwapChainPanel _panel;
    private readonly DesktopCapture _capture;

    private IDXGIFactory2? _factory;
    private IDXGISwapChain1? _swapChain;
    private ID3D11Texture2D? _backBuffer;
    private ID3D11DeviceContext? _context;
    private bool _disposed;

    /// <summary>Total frames presented (thread-safe counter for FPS display).</summary>
    public long PresentedFrames => Interlocked.Read(ref _presented);
    private long _presented;

    public CaptureRenderer(SwapChainPanel panel, DesktopCapture capture)
    {
        _panel = panel;
        _capture = capture;
    }

    public void Initialize()
    {
        ID3D11Device device = _capture.Device;
        _context = device.ImmediateContext;

        SizeInt32 size = _capture.Item.Size;

        // Composition swapchain sized to the captured monitor.
        _factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>().QueryInterface<IDXGIFactory2>();
        var description = new SwapChainDescription1
        {
            Width = (uint)size.Width,
            Height = (uint)size.Height,
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

        // GPU -> GPU: every captured frame is copied and presented directly.
        _capture.FrameArrived += OnFrameArrived;
    }

    private void OnFrameArrived(ID3D11Texture2D texture, SizeInt32 contentSize)
    {
        if (_disposed || _backBuffer is null || _context is null)
        {
            return;
        }

        // Same-size blit on the GPU. ContentSize can lag the pool size for one
        // frame after a resolution change; clip defensively.
        _context.CopyResource(_backBuffer, texture);
        _swapChain!.Present(1, 0);
        Interlocked.Increment(ref _presented);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _capture.FrameArrived -= OnFrameArrived;
        try { _backBuffer?.Dispose(); } catch { }
        try { _swapChain?.Dispose(); } catch { }
        try { _factory?.Dispose(); } catch { }
        _backBuffer = null;
        _swapChain = null;
        _factory = null;
        _context = null; // owned by the device
    }
}
