using System;
using System.Collections.Generic;
using DuoFlow.Capture;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace DuoFlow.App;

/// <summary>
/// M0.3 overlay window: borderless, covering the whole primary monitor,
/// always on top, fully transparent (DWM frame extension + transparent
/// XAML root), click-through and non-activating (WS_EX_TRANSPARENT |
/// WS_EX_NOACTIVATE), hidden from Alt+Tab (WS_EX_TOOLWINDOW).
///
/// The M0.2 desktop-capture chain is rendered inside it as the first
/// piece of "effect" content.
/// </summary>
public sealed partial class OverlayWindow : Window
{
    private DesktopCapture? _capture;
    private CaptureRenderer? _renderer;
    private bool _cleanupDone;

    /// <summary>DWM frame extension result (true == transparent client area).</summary>
    public bool DwmExtended { get; private set; }

    /// <summary>Capture chain state, surfaced to the console/CI report.</summary>
    public string CaptureState { get; private set; } = "starting";

    /// <summary>Non-fatal setup issues (style bits that could not be set, etc.).</summary>
    public List<string> Warnings { get; } = new();

    public CaptureRenderer? Renderer => _renderer;

    public OverlayWindow()
    {
        InitializeComponent();

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // 1. Borderless + always-on-top + fullscreen covering the primary monitor.
        try
        {
            AppWindow appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            RectInt32 screen = DisplayArea.Primary.OuterBounds;
            appWindow.MoveAndResize(screen);
        }
        catch (Exception ex)
        {
            Warnings.Add($"presenter/fullscreen: {ex.Message}");
        }

        // 2. Click-through + never steal focus + hidden from Alt+Tab.
        try
        {
            long exStyle = OverlayNative.GetExStyle(hwnd);
            OverlayNative.SetExStyle(hwnd,
                exStyle
                    | OverlayNative.WS_EX_TRANSPARENT
                    | OverlayNative.WS_EX_NOACTIVATE
                    | OverlayNative.WS_EX_TOOLWINDOW);
            OverlayNative.ForceTopmost(hwnd);
        }
        catch (Exception ex)
        {
            Warnings.Add($"exstyle: {ex.Message}");
        }

        // 3. Fully transparent client area (DWM glass frame over everything).
        try
        {
            DwmExtended = OverlayNative.ExtendFrame(hwnd) == 0;
            if (!DwmExtended)
            {
                Warnings.Add("dwm: ExtendFrameIntoClientArea failed");
            }
        }
        catch (Exception ex)
        {
            Warnings.Add($"dwm: {ex.Message}");
        }

        ((FrameworkElement)Content).Loaded += OnLoaded;
        Closed += (_, _) => Cleanup();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _capture = new DesktopCapture();
            _capture.Start();

            _renderer = new CaptureRenderer(CapturePanel, _capture);
            _renderer.Initialize();

            SizeInt32 size = _capture.Item.Size;
            OverlayCaption.Text =
                $"overlay · {_capture.MonitorDescription} · {size.Width}×{size.Height} · GPU→GPU";
            CaptureState = "running";
        }
        catch (Exception ex)
        {
            CaptureState = $"failed: {ex.GetType().Name}: {ex.Message}";
            OverlayCaption.Text = "capture failed (see console)";
        }
    }

    private void Cleanup()
    {
        if (_cleanupDone)
        {
            return;
        }
        _cleanupDone = true;

        _renderer?.Dispose();
        _capture?.Dispose();
        _renderer = null;
        _capture = null;
    }
}
