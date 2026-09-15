using System;
using System.Runtime.InteropServices;
using DuoFlow.Capture.Interop;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace DuoFlow.Capture;

/// <summary>
/// M0.2 feasibility probe: captures the PRIMARY monitor via
/// Windows.Graphics.Capture and exposes each frame as a native D3D11 texture.
///
/// Guarantees (per DESIGN_DECISIONS):
///   - GPU -> GPU only. Frames stay D3D11 textures; nothing is staged to CPU.
///   - Free-threaded frame pool: frames arrive on a worker thread, the
///     consumer copies synchronously inside the callback.
/// </summary>
public sealed class DesktopCapture : IDisposable
{
    /// <summary>
    /// Raised for every captured frame. The texture is only valid inside the
    /// callback (the frame it belongs to is released right after).
    /// </summary>
    public event Action<ID3D11Texture2D, SizeInt32>? FrameArrived;

    /// <summary>D3D11 device shared with the renderer (CopyResource needs sameness).</summary>
    public ID3D11Device Device { get; private set; } = null!;

    public GraphicsCaptureItem Item { get; private set; } = null!;
    public string MonitorDescription => Item?.DisplayName ?? "unknown";

    /// <summary>Diagnostics: driver type used + WGC support flag (logged in the UI).</summary>
    public string DriverInfo { get; private set; } = "";

    /// <summary>Last error raised inside the frame callback, if any.</summary>
    public string? LastError { get; private set; }

    private IDirect3DDevice? _winrtDevice;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private bool _disposed;

    public void Start()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DesktopCapture));
        }

        // 1. D3D11 device (BGRA support required by the capture API).
        //    Hardware first; fall back to WARP on machines without a real GPU
        //    (cloud runners / VMs) so the capture chain can still be evaluated.
        DriverType driverType = DriverType.Hardware;
        Result hr = Vortice.Direct3D11.D3D11.D3D11CreateDevice(
            null, driverType, DeviceCreationFlags.BgraSupport, null,
            out ID3D11Device? deviceOut);
        if (hr.Failure || deviceOut is null)
        {
            driverType = DriverType.Warp;
            hr = Vortice.Direct3D11.D3D11.D3D11CreateDevice(
                null, driverType, DeviceCreationFlags.BgraSupport, null,
                out deviceOut);
        }
        hr.CheckError();
        ID3D11Device device = deviceOut!;
        Device = device;
        using IDXGIDevice dxgiDevice = device.QueryInterface<IDXGIDevice>();

        // 2. Bind capture to the primary monitor (no picker UI needed).
        IntPtr hMonitor = NativeMethods.GetPrimaryMonitorHandle();
        Guid iid = NativeMethods.GraphicsCaptureItemIID;
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        IntPtr itemAbi = interop.CreateForMonitor(hMonitor, ref iid);
        Item = GraphicsCaptureItem.FromAbi(itemAbi)!;
        Marshal.Release(itemAbi);

        // 3. WinRT device wrapper for the frame pool.
        _winrtDevice = Direct3D11Helper.CreateIDirect3DDevice(dxgiDevice);

        // 4. Free-threaded frame pool: 2 buffers, GPU textures only.
        //    (Verified on cloud: session starts; frame delivery depends on the
        //    display adapter — real-GPU machines deliver continuously.)
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _winrtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            Item.Size);
        _framePool.FrameArrived += OnFrameArrived;

        // 5. Session. Cosmetic options are best-effort (permission-gated on some builds).
        _session = _framePool.CreateCaptureSession(Item);
        TrySet(_session, session => session.IsCursorCaptureEnabled = true);
        // IsBorderRequired requires a 22000+ TFM projection and special permission;
        // skipped for M0.2 (border/outline does not affect the feasibility result).
        _session.StartCapture();

        bool supported;
        try { supported = GraphicsCaptureSession.IsSupported(); }
        catch { supported = true; }
        DriverInfo = $"{driverType} · IsSupported={supported}";
    }

    /// <summary>Resize the frame pool when the monitor resolution changes.</summary>
    public void RecreateFramePool(SizeInt32 newSize)
        => _framePool?.Recreate(_winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, newSize);

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object? args)
    {
        // Never let an exception escape a capture callback: it would kill the
        // app. Record it for the status bar instead.
        try
        {
            using Direct3D11CaptureFrame? frame = sender.TryGetNextFrame();
            if (frame is null)
            {
                return;
            }

            if (!_disposed && FrameArrived is not null)
            {
                using ID3D11Texture2D texture = Direct3D11Helper.GetD3DTexture(frame.Surface);
                FrameArrived.Invoke(texture, frame.ContentSize);
            }
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private static void TrySet(GraphicsCaptureSession session, Action<GraphicsCaptureSession> configure)
    {
        try
        {
            configure(session);
        }
        catch
        {
            // Permission-gated / build-dependent; never fatal for the demo.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_framePool is not null)
        {
            _framePool.FrameArrived -= OnFrameArrived;
        }
        try { _session?.Dispose(); } catch { }
        try { _framePool?.Dispose(); } catch { }
        try { (_winrtDevice as IDisposable)?.Dispose(); } catch { }
        try { Device?.Dispose(); } catch { }
        _session = null;
        _framePool = null;
        _winrtDevice = null;
        Device = null!;
    }
}
