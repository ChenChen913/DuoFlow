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
/// Display path: renders the captured desktop through the effect pipeline
/// (M1.4 warp + M1.5 mask + M1.6 blur + M1.7 dim - DD-039..DD-042) into a
/// composition swapchain bound to a SwapChainPanel.
///
/// M1.8 architecture (DD-044, TECHNICAL_PROPOSAL §22): CAPTURE and RENDER
/// are decoupled. The WGC frame callback only refreshes a persistent GPU
/// copy of the latest desktop frame; a render heartbeat (RenderTick, called
/// from the UI thread) renders at its own pace from that copy - so the
/// composite keeps animating even when the screen is static and the capture
/// delivers no frames (a static screen produces no WGC frames, which at
/// fullscreen froze the old render-in-callback loop at 1 FPS).
///
/// M1.8 fullscreen: the SwapChainPanel covers the whole overlay and the
/// swapchain is sized to the panel's physical pixels (= the capture size on
/// this machine), satisfying the DPI-scaling workaround (HC 6.2) at zero
/// downscale. The composite is semi-transparent (GlobalOpacity): the capture
/// contains our own previous output, and a sub-unity opacity makes that
/// feedback converge into a stable frosted-glass blend instead of compounding
/// without bound.
/// </summary>
public sealed class CaptureRenderer : IDisposable
{
    private readonly SwapChainPanel _panel;
    private readonly DesktopCapture _capture;
    private readonly LidAnimationClock? _clock;

    private IDXGIFactory2? _factory;
    private IDXGISwapChain1? _swapChain;
    private ID3D11Texture2D? _backBuffer;
    private ID3D11Texture2D? _latestFrame;
    private ID3D11ShaderResourceView? _latestFrameSrv;
    private ID3D11DeviceContext? _context;
    private WarpPipeline? _warp;

    // M2.5: the tuning options are REPLACEABLE at runtime (the console's
    // parameter panel swaps whole immutable instances). Both WarpOptions and
    // BlurOptions are init-only, so a change means "build a new instance",
    // never "mutate in place" - that is what makes the swap atomic with
    // respect to a frame: RenderTick reads the fields ONCE into locals at the
    // top of the fold branch, so a mid-frame swap cannot produce a frame
    // stitched from two option sets.
    //
    // Threading: SetOptions is called from the UI thread (slider handlers)
    // and RenderTick also runs on the UI thread (DispatcherQueueTimer), so
    // there is nothing to lock. _renderGate guards the GPU resources shared
    // with the capture thread, not these fields.
    private WarpOptions _warpOptions = new();
    private BlurOptions _blurOptions = new();
    private GradientOptions _gradientOptions = new();
    private readonly object _renderGate = new();
    private DumpRequest? _pendingDumpRequest;
    private bool _sourceFrozen; // DD-045: true while the fold is on screen (capture frozen)
    private int _panelPixelWidth;
    private int _panelPixelHeight;
    private bool _disposed;

    // Display-topology handling (2026-10-22): the capture surface and the
    // panel can change size at runtime (resolution change, DPI change,
    // docking/undocking). Every GPU target sized to the old geometry must be
    // rebuilt; the flag defers the work to the render heartbeat so the
    // rebuild happens inside the render gate, never mid-frame.
    private bool _rebuildPending;
    private int _swapWidth;
    private int _swapHeight;

    // Frame-budget guard (2026-10-22): RenderTick runs on the UI thread, so a
    // frame that overruns the heartbeat interval stalls input. The renderer
    // measures its own frame time and scales the frosted radius down when the
    // average overruns the budget (never below MinQualityScale) - graceful
    // degradation instead of a frozen console.
    private const double FrameBudgetMs = 25.0;
    private const double MinQualityScale = 0.5;
    private readonly System.Diagnostics.Stopwatch _frameWatch = new();
    private double _avgFrameMs = 8.0;
    private double _qualityScale = 1.0;

    /// <summary>
    /// Global opacity of the fullscreen composite. 1.0 (DD-045): the fold
    /// renders from the FROZEN clean desktop (no capture feedback), so it can
    /// be fully opaque - no ghost, no melt.
    /// </summary>
    public const double GlobalOpacity = 1.0;

    /// <summary>Progress over which the composite fades in from invisible
    /// (progress 0 = fully open = no effect over the live desktop).</summary>
    public const double FadeInRange = 0.15;

    /// <summary>Total frames presented (thread-safe counter for FPS display).</summary>
    public long PresentedFrames => Interlocked.Read(ref _presented);
    private long _presented;

    /// <summary>M1.4 warp pipeline state, surfaced to the console/probe
    /// (informational; the real-machine checks are the visual authority).</summary>
    public string WarpState { get; private set; } = "starting";

    /// <summary>
    /// M2.5: replace the tuning options at runtime (console parameter panel).
    /// MUST be called on the UI thread - the same thread as RenderTick.
    ///
    /// Validation happens HERE, not in the shader: an out-of-range value would
    /// throw inside WarpGeometry.Compute on the render heartbeat, which is a
    /// fire-and-forget timer tick - the exception would surface as a dead
    /// overlay with no obvious cause. So the candidate is validated once and
    /// rejected as a whole (all-or-nothing, no half-applied state).
    /// </summary>
    /// <returns>null on success, otherwise a human-readable rejection reason.</returns>
    public string? SetOptions(WarpOptions warp, BlurOptions blur, GradientOptions gradient)
    {
        if (warp is null) { return "warp options null"; }
        if (blur is null) { return "blur options null"; }
        if (gradient is null) { return "gradient options null"; }

        try
        {
            // Both validators are the ones the render path uses - call them
            // with the same arguments the renderer would, at both sweep ends
            // so a combination that only breaks at progress 1 is caught too.
            WarpGeometry.Compute(0.0, warp);
            WarpGeometry.Compute(1.0, warp);
            blur.Validate();
            gradient.Validate();
        }
        catch (Exception ex)
        {
            Trace.Log($"tuning: REJECTED ({ex.GetType().Name}: {ex.Message})");
            return ex.Message;
        }

        _warpOptions = warp;
        _blurOptions = blur;
        _gradientOptions = gradient;

        // Trace the DERIVED geometry, not just the inputs: the acceptance
        // check greps M00 at full fold, which is r*cos(MaxFoldDegrees) - the
        // one number that proves the panel actually reached the render math
        // (and not just the UI). 60 deg / r=2.5 -> M00=1.2500.
        WarpFrame atFull = WarpGeometry.Compute(1.0, warp);
        Trace.Log($"tuning: warp maxFold={warp.MaxFoldDegrees:0.0} r={warp.CameraDistanceRatio:0.00} " +
                  $"tiltAway={warp.TiltAwayFromViewer} hingeTop={warp.HingeAtTop} feather={warp.EdgeFeather:0.0000} " +
                  $"-> frame@1.0 M00={atFull.M00:0.0000} M11={atFull.M11:0.0000} M21={atFull.M21:0.0000} " +
                  $"farEdgeY={atFull.FarEdgeY:0.0000}");
        Trace.Log($"tuning: blur max={blur.MaxBlurPixels:0.0} start={blur.StartProgress:0.00} full={blur.FullProgress:0.00}");
        Trace.Log($"tuning: gradient enabled={gradient.Enabled} strength={gradient.EffectiveStrength:0.000} " +
                  $"hue={gradient.HueStartDegrees:0}->{gradient.HueEndDegrees:0}");
        return null;
    }

    private sealed class DumpRequest
    {
        public string Path { get; init; } = "";
    }

    /// <summary>M1.4 self-test support: request a one-shot staging readback of
    /// the next rendered frame into a raw BGRA file. Executed on the render
    /// thread inside the render gate, so the immediate context is never
    /// touched from two threads.</summary>
    public void RequestDump(string filePath)
        => Interlocked.Exchange(ref _pendingDumpRequest, new DumpRequest { Path = filePath });

    public CaptureRenderer(SwapChainPanel panel, DesktopCapture capture, LidAnimationClock? clock = null)
    {
        _panel = panel;
        _capture = capture;
        _clock = clock;

        // Runtime geometry changes: WGC reports the new capture size on its
        // own thread, the panel reports DIP size changes on the UI thread.
        // Both funnel into one deferred rebuild.
        _capture.SizeChanged += OnCaptureSizeChanged;
        _panel.SizeChanged += OnPanelSizeChanged;
    }

    private void OnCaptureSizeChanged(SizeInt32 size)
    {
        // SwapChainPanel interop is UI-thread affine: marshal, then let the
        // heartbeat perform the actual rebuild inside the render gate.
        _panel.DispatcherQueue?.TryEnqueue(() => _rebuildPending = true);
    }

    private void OnPanelSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        => _rebuildPending = true;

    /// <summary>
    /// Average render frame time (ms, exponential moving average) and the
    /// quality scale currently applied - surfaced for diagnostics/M8.
    /// </summary>
    public double AverageFrameMs => _avgFrameMs;

    /// <summary>Frosted-radius multiplier the frame budget has settled on (0.5~1.0).</summary>
    public double QualityScale => _qualityScale;

    public void Initialize(int panelPixelWidth = 0, int panelPixelHeight = 0)
    {
        _panelPixelWidth = panelPixelWidth;
        _panelPixelHeight = panelPixelHeight;

        ID3D11Device device = _capture.Device;
        _context = device.ImmediateContext;

        CreateTargets();

        _capture.FrameArrived += OnFrameArrived;
    }

    /// <summary>
    /// Allocates every GPU target sized to the current capture/panel
    /// geometry: the persistent capture copy (with its mip chain), the
    /// composition swapchain, its back buffer and the warp pipeline.
    /// Called once at startup and again on every geometry change.
    /// </summary>
    private void CreateTargets()
    {
        ID3D11Device device = _capture.Device;
        SizeInt32 size = _capture.Item.Size;

        // Persistent GPU copy of the latest captured frame (M1.8: the render
        // heartbeat samples THIS texture, decoupling render from capture rate).
        // Model v2 (DD-046): full MIP chain + GenerateMips - the frosted blur
        // pre-filters through mip levels (velvet, no mosaic). CopyResource
        // cannot resize, so each capture frame lands via CopySubresourceRegion
        // into level 0 and GenerateMips rebuilds the chain.
        int mipLevels = (int)Math.Floor(Math.Log2(Math.Max(size.Width, size.Height))) + 1;
        _latestFrame = device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)size.Width,
            Height = (uint)size.Height,
            MipLevels = (uint)mipLevels,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.GenerateMips,
        });
        _latestFrameSrv = device.CreateShaderResourceView(_latestFrame, null);

        // Composition swapchain sized to the PANEL'S PIXEL SIZE (2026-09-19
        // DPI-scaling workaround, HC 6.2); with the fullscreen panel this
        // coincides with the capture size (zero downscale, zero upscale).
        SizeInt32 swapSize = size;
        if (_panelPixelWidth > 0 && _panelPixelHeight > 0)
        {
            swapSize = new SizeInt32(_panelPixelWidth, _panelPixelHeight);
        }
        _factory ??= DXGI.CreateDXGIFactory1<IDXGIFactory1>().QueryInterface<IDXGIFactory2>();
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
        using (var panelNative = new Vortice.WinUI.ISwapChainPanelNative(_panel))
        {
            panelNative.SetSwapChain(_swapChain);
        }

        _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _swapWidth = swapSize.Width;
        _swapHeight = swapSize.Height;

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
            using (var panelNative2 = new Vortice.WinUI.ISwapChainPanelNative(_panel))
            {
                panelNative2.SetSwapChain(_swapChain);
            }
            _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            _swapWidth = size.Width;
            _swapHeight = size.Height;
        }
        WarpState = _warp is null ? "blit fallback (warp init failed)" : "active";
        Trace.Log($"capture renderer: warp state = {WarpState} (capture {size.Width}×{size.Height}, swapchain {_swapWidth}×{_swapHeight})");
    }

    /// <summary>
    /// Releases every geometry-bound target and rebuilds them for the new
    /// capture/panel size. Runs inside the render gate only.
    /// </summary>
    private void RebuildTargets()
    {
        try
        {
            _warp?.Dispose();
            _latestFrameSrv?.Dispose();
            _latestFrame?.Dispose();
            _backBuffer?.Dispose();
            _swapChain?.Dispose();
            _warp = null;
            _latestFrameSrv = null;
            _latestFrame = null;
            _backBuffer = null;
            _swapChain = null;

            // Re-read the panel's PHYSICAL size: a DPI change moves
            // RasterizationScale without necessarily changing ActualWidth.
            double scale = _panel.XamlRoot?.RasterizationScale ?? 1.0;
            _panelPixelWidth = Math.Max(1, (int)Math.Round(_panel.ActualWidth * scale));
            _panelPixelHeight = Math.Max(1, (int)Math.Round(_panel.ActualHeight * scale));

            CreateTargets();

            // The frozen source belongs to the OLD geometry - start clean.
            _sourceFrozen = false;
            Trace.Log($"capture renderer: rebuilt for new geometry (panel {_panelPixelWidth}×{_panelPixelHeight}, scale {scale:0.00})");
        }
        catch (Exception ex)
        {
            WarpState = "rebuild failed";
            Trace.Log($"capture renderer: rebuild FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>M1.8: the render heartbeat. Called from the UI thread on a
    /// ~30ms timer - renders the composite from the persistent latest-frame
    /// copy regardless of whether the capture delivered anything (a static
    /// screen produces no WGC frames). Everything runs inside the gate; the
    /// capture callback shares it.</summary>
    public void RenderTick()
    {
        if (_disposed || _context is null || _backBuffer is null || _latestFrame is null)
        {
            return;
        }

        lock (_renderGate)
        {
            if (_disposed)
            {
                return;
            }

            // Deferred geometry rebuild (resolution/DPI/topology change).
            // Done here so it always lands inside the gate, between frames.
            if (_rebuildPending)
            {
                _rebuildPending = false;
                RebuildTargets();
                if (_backBuffer is null || _latestFrame is null)
                {
                    return; // rebuild failed - next tick retries nothing, stay dark
                }
            }

            // Frame-budget guard (measure THIS tick's work, not the gap
            // between ticks - the heartbeat interval is 30ms by design and
            // must not be mistaken for render cost).
            _frameWatch.Restart();

            DumpRequest? request = _pendingDumpRequest;
            _pendingDumpRequest = null;

            if (_warp is { } warp && _clock is { } clock)
            {
                // M1.4-M1.8 model v2: engine Tick -> smoothed progress ->
                // warp + physical gap blur/light (DD-046) -> fullscreen
                // composite. The AnimationEngine is the ONLY progress source
                // (DD-002/DD-038); the clock serializes the UI thread's
                // Update() against this thread.
                LidState state = clock.Tick();

                // Read the tuning options ONCE per frame (see the field
                // comment): a swap mid-frame can then never mix two sets.
                WarpOptions warpOptions = _warpOptions;
                BlurOptions blurOptions = _blurOptions;
                GradientOptions gradientOptions = _gradientOptions;
                WarpFrame frame = WarpGeometry.Compute(state.Progress, warpOptions);

                // M1.8 feedback kill (DD-045): the fullscreen capture contains
                // our own previous output - sampling it while the effect is
                // visible ghosts (folds the fold, every frame). Freeze the
                // source at the last progress-0 capture (taken while the
                // overlay was invisible = clean desktop) and render the fold
                // from THAT until the fold returns to fully open.
                if (state.Progress <= 0.001)
                {
                    _sourceFrozen = false;
                }
                else if (!_sourceFrozen)
                {
                    _sourceFrozen = true;
                    Trace.Log("capture renderer: effect visible - capture source frozen (DD-045)");
                }

                double fadeOpacity =
                    Math.Clamp(state.Progress / FadeInRange, 0.0, 1.0) * GlobalOpacity;
                warp.Render(_context, _latestFrame, frame,
                    blurOptions.MaxBlurPixels * _qualityScale,
                    blurOptions.IntensityAt(state.Progress), fadeOpacity,
                    gradientOptions);
            }
            else
            {
                // M0.2 fallback: same-size blit on the GPU.
                _context.CopyResource(_backBuffer, _latestFrame);
            }

            if (request is not null)
            {
                DumpBackBuffer(request.Path);
            }

            _swapChain?.Present(1, 0);
            Interlocked.Increment(ref _presented);

            // Frame budget: elapsed covers this tick's shader work + Present
            // (the vsync wait IS UI-thread time and must count). Above budget
            // the frosted radius shrinks step by step; well below it, it
            // recovers. Degrade quality, never skip frames - skipping would
            // break the "continuous Progress" guarantee.
            double workMs = _frameWatch.Elapsed.TotalMilliseconds;
            _avgFrameMs = _avgFrameMs * 0.9 + workMs * 0.1;
            if (_avgFrameMs > FrameBudgetMs)
            {
                _qualityScale = Math.Max(MinQualityScale, _qualityScale - 0.05);
            }
            else if (_avgFrameMs < FrameBudgetMs * 0.6)
            {
                _qualityScale = Math.Min(1.0, _qualityScale + 0.02);
            }
        }
    }

    private void OnFrameArrived(ID3D11Texture2D texture, SizeInt32 contentSize)
    {
        if (_disposed || _latestFrame is null)
        {
            return;
        }

        // M1.8: refresh the persistent latest-frame copy only while the
        // effect is INVISIBLE (progress 0). Once the fold is on screen the
        // capture contains our own output - sampling it ghosts (DD-045), so
        // the source stays frozen at the last clean desktop until the fold
        // fully opens again. Level 0 only (CopyResource needs identical
        // chains); GenerateMips rebuilds the rest for the frost pre-filter.
        // Rendering happens on the heartbeat (RenderTick), decoupled from the
        // capture rate (TECHNICAL_PROPOSAL §22).
        lock (_renderGate)
        {
            if (_disposed || _sourceFrozen)
            {
                return;
            }
            _context?.CopySubresourceRegion(_latestFrame, 0, 0, 0, 0, texture, 0, null);
            _context?.GenerateMips(_latestFrameSrv!);
        }
    }

    /// <summary>Staging readback of the whole back buffer into a raw BGRA
    /// file (width×height×4 bytes, top-down rows). Runs inside the render
    /// gate only.</summary>
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

        _capture.FrameArrived -= OnFrameArrived;
        _capture.SizeChanged -= OnCaptureSizeChanged;
        _panel.SizeChanged -= OnPanelSizeChanged;

        // Serialize against an in-flight capture callback: it holds the gate
        // while copying into _latestFrame, so disposing outside the lock
        // could pull the texture out from under it.
        lock (_renderGate)
        {
            _disposed = true;
            try { _warp?.Dispose(); } catch { }
            try { _latestFrameSrv?.Dispose(); } catch { }
            try { _latestFrame?.Dispose(); } catch { }
            try { _backBuffer?.Dispose(); } catch { }
            try { _swapChain?.Dispose(); } catch { }
            try { _factory?.Dispose(); } catch { }
            _warp = null;
            _latestFrameSrv = null;
            _latestFrame = null;
            _backBuffer = null;
            _swapChain = null;
            _factory = null;
            _context = null; // owned by the device
        }
    }
}
