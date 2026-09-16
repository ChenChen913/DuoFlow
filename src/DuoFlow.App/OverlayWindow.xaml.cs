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
/// always on top, fully transparent, click-through and non-activating
/// (WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE), hidden from
/// Alt+Tab (WS_EX_TOOLWINDOW).
///
/// P0 fixes (real-machine findings, 2026-09-16; both verified PASS on the
/// real machine the same day - see DD-037 / HARDWARE_COMPATIBILITY §6.2):
///  - P0-1 transparency: BOTH background layers must be handled - the Win32
///    layer (DwmExtendFrameIntoClientArea with MARGINS(0) + blur-behind with
///    an empty region) and the XAML island layer (an alpha-0 brush assigned
///    DIRECTLY on the window's ICompositionSupportsSystemBackdrop interface
///    via WinRT.CastExtensions.As<> - no SystemBackdrop subclass, which was
///    CI-disproven; see OverlayBackdrop.cs). The old DwmExtendFrame(-1)-only
///    approach left the island opaque black and hid the whole desktop.
///  - P0-2 click-through: WS_EX_LAYERED is REQUIRED for the hit-test
///    exclusion; WS_EX_TRANSPARENT alone still lets the
///    DesktopChildSiteBridge swallow all mouse input.
///
/// The M0.2 desktop-capture chain is rendered inside it as the first
/// piece of "effect" content.
/// </summary>
public sealed partial class OverlayWindow : Window
{
    private DesktopCapture? _capture;
    private CaptureRenderer? _renderer;
    private bool _cleanupDone;

    /// <summary>Win32-layer transparency applied (MARGINS(0) + blur-behind).</summary>
    public bool DwmExtended { get; private set; }

    /// <summary>P0-1: alpha-0 backdrop brush connected via window.As&lt;ICompositionSupportsSystemBackdrop&gt;.</summary>
    public bool BackdropApplied { get; private set; }

    /// <summary>P0-2: WS_EX_LAYERED set AND read back from the real window.</summary>
    public bool LayeredApplied { get; private set; }

    /// <summary>Capture chain state, surfaced to the console/CI report.</summary>
    public string CaptureState { get; private set; } = "starting";

    /// <summary>Non-fatal setup issues (style bits that could not be set, etc.).</summary>
    public List<string> Warnings { get; } = new();

    public CaptureRenderer? Renderer => _renderer;

    public OverlayWindow()
    {
        Trace.Log("overlay: ctor begin");
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
            Trace.Log($"overlay: fullscreen OK {screen.X},{screen.Y} {screen.Width}×{screen.Height}");
        }
        catch (Exception ex)
        {
            Warnings.Add($"presenter/fullscreen: {ex.Message}");
            Trace.Log($"overlay: presenter/fullscreen FAILED: {ex.Message}");
        }

        // 2. Click-through + never steal focus + hidden from Alt+Tab.
        //    P0-2: WS_EX_LAYERED is mandatory for real hit-test exclusion
        //    (content island); TRANSPARENT alone was proven insufficient on
        //    the real machine. Layered attributes are initialized with
        //    LWA_ALPHA(255) - the COLOR KEY variant (LWA_COLORKEY) was tried
        //    and REVERTED (CI 35077849717: the island's DComp background
        //    never reaches a GDI surface, so the key color is never punched
        //    out, and the colorkey combo broke click-through).
        try
        {
            OverlayNative.EnableLayeredClickThrough(hwnd);

            LayeredApplied = (OverlayNative.GetExStyle(hwnd) & OverlayNative.WS_EX_LAYERED) != 0;
            if (!LayeredApplied)
            {
                Warnings.Add("exstyle: WS_EX_LAYERED could not be applied (P0-2)");
            }
            Trace.Log($"overlay: exstyle OK layered={LayeredApplied} (LWA_ALPHA; colorkey tried and reverted)");
        }
        catch (Exception ex)
        {
            Warnings.Add($"exstyle: {ex.Message}");
            Trace.Log($"overlay: exstyle FAILED: {ex.Message}");
        }

        // 3. Fully transparent client area - both layers (P0-1).
        //    a) XAML island layer: alpha-0 brush assigned DIRECTLY on the
        //       window's OS backdrop interface (cnbluefire recipe) - a
        //       SystemBackdrop subclass connected an identical brush but the
        //       screen stayed black (run 35077212761); the direct path is the
        //       one the reference implementation ships.
        try
        {
            BackdropApplied = OverlayTransparency.Apply(this);
            if (!BackdropApplied)
            {
                Warnings.Add("transparency: island backdrop brush failed");
            }
        }
        catch (Exception ex)
        {
            BackdropApplied = false;
            Warnings.Add($"transparency: {ex.Message}");
            Trace.Log($"overlay: island transparency FAILED: {ex.Message}");
        }

        //    b) Win32 surface layer: DwmExtendFrameIntoClientArea(MARGINS(0))
        //       + blur-behind with an EMPTY region - the degenerate region
        //       marks the window for DWM per-pixel alpha without drawing any
        //       real blur. (A WM_PAINT fill inside the subclass was TRIED AND
        //       REVERTED on the layered window - CI 35079027989 - so the
        //       subclass only re-applies on WM_DWMCOMPOSITIONCHANGED.)
        try
        {
            DwmExtended = OverlayNative.ApplyTransparentWin32Layer(hwnd) == 0;
            if (!DwmExtended)
            {
                Warnings.Add("dwm: transparent win32 layer failed");
            }
            OverlayWin32Subclass.Install(hwnd);
            Trace.Log($"overlay: win32 transparent layer applied={DwmExtended}");
        }
        catch (Exception ex)
        {
            Warnings.Add($"dwm: {ex.Message}");
            Trace.Log($"overlay: dwm FAILED: {ex.Message}");
        }

        ((FrameworkElement)Content).Loaded += OnLoaded;
        Closed += (_, _) => Cleanup();
        Trace.Log("overlay: ctor done");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Trace.Log("overlay: loaded");
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
            Trace.Log("overlay: capture running");
        }
        catch (Exception ex)
        {
            CaptureState = $"failed: {ex.GetType().Name}: {ex.Message}";
            OverlayCaption.Text = "capture failed (see console)";
            Trace.Log($"overlay: capture FAILED: {CaptureState}");
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
