using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DuoFlow.Capture;
using DuoFlow.Core;
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
    private readonly bool _selfTest;
    private bool _cleanupDone;

    /// <summary>
    /// M1.4: the smoothed lid clock (AnimationEngine behind a small lock).
    /// The console forwards ManualProvider states here (UI thread); the
    /// capture render loop Ticks it (frame-pool worker thread).
    /// </summary>
    public LidAnimationClock Clock { get; } = new();

    /// <summary>M1.4 warp pipeline state (informational; the console and the
    /// smoke JSON surface it, the CI gate does not assert on it).</summary>
    public string WarpState => _renderer?.WarpState ?? "off (no renderer)";

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

    public OverlayWindow(bool selfTest = false)
    {
        _selfTest = selfTest;
        Trace.Log($"overlay: ctor begin (selfTest={selfTest})");
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

    /// <summary>
    /// M1.4: feed a raw provider state into the animation clock (called on
    /// the UI thread by the console window; DD-035 ManualProvider semantics
    /// untouched - the engine sits downstream, DD-038).
    /// </summary>
    public void NotifyLidState(LidState state) => Clock.Update(state);

    // ------------------------------------------------------------------
    // M1.4 self-test (--warp-selftest): drives the animation engine through
    // a 0→1→0 sweep and dumps the RENDERED back buffer at each step.
    //
    // Purpose: the machine's desktop-composition path currently has a
    // pre-existing issue (SwapChainPanel content invisible on screen,
    // reproduced with the pre-M1.4 baseline - see HANDOFF-M1.4-wip.md), so
    // M1.4 acceptance runs IN-PROCESS: the dumped frames contain the warp
    // output INCLUDING alpha, and the analysis script compares band-boundary
    // positions against the WarpGeometry homography prediction.
    //
    // A fullscreen color-band reference window must be on screen while this
    // runs - the desktop capture is the warp's source texture.
    // ------------------------------------------------------------------

    private async Task RunWarpSelfTestAsync()
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "duoflow-warp-selftest");
            Directory.CreateDirectory(dir);

            double[] steps = { 0.0, 0.25, 0.5, 0.75, 1.0, 0.75, 0.5, 0.25, 0.0 };
            string[] tags = { "up0", "up1", "up2", "up3", "up4", "down1", "down2", "down3", "down4" };

            Trace.Log("selftest: begin (settle 3s)");
            // Hide the overlay's own decorations during the sweep: the capture
            // otherwise contains the panel (recursion), the console and the
            // status chip, polluting the dumped frames. The swapchain keeps
            // rendering while collapsed - the readback sees the pure warp
            // output, and the capture sees the clean reference window.
            Root.Visibility = Visibility.Collapsed;
            await Task.Delay(3000);

            for (int i = 0; i < steps.Length; i++)
            {
                double p = steps[i];
                NotifyLidState(RawLidState(p));

                await Task.Delay(2500); // engine slew (rate cap 2.0/s) + settle

                string path = Path.Combine(dir, $"selftest-{tags[i]}.raw");
                _renderer?.RequestDump(path);
                Trace.Log($"selftest: step {tags[i]} p={p:0.00} requested " +
                          $"(engine now {Clock.CurrentProgress:0.000} v={Clock.CurrentVelocity:0.000})");
                await Task.Delay(1500); // let a rendered frame carry the dump out
            }

            // M1.5: hinge-mask debug phase (DD-040). The mask is
            // progress-independent; the debug dumps show it through the
            // shader's heat ramp (R channel = mask value exactly).
            //   p=0   -> identity quad: the FULL frame is mask, so the
            //            profile can be decoded row by row against theory.
            //   p=0.5 -> the folded quad clips the mask (alpha boundary at
            //            the known FarEdgeY) - quad/mask coupling evidence.
            NotifyLidState(RawLidState(0.0));
            await Task.Delay(2500);
            _renderer?.RequestDump(Path.Combine(dir, "selftest-mask-p0.raw"), debugMask: true);
            Trace.Log("selftest: mask debug dump @p=0 requested");
            await Task.Delay(1500);

            NotifyLidState(RawLidState(0.5));
            await Task.Delay(2500);
            _renderer?.RequestDump(Path.Combine(dir, "selftest-mask-p50.raw"), debugMask: true);
            Trace.Log("selftest: mask debug dump @p=0.5 requested");
            await Task.Delay(1500);

            Trace.Log("selftest: DONE");
        }
        catch (Exception ex)
        {
            Trace.Log($"selftest: FAILED: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Root.Visibility = Visibility.Visible; // restore the overlay UI
        }
    }

    private static LidState RawLidState(double p) => new(
        Angle: ManualProvider.DefaultOpenAngleDegrees
               - (ManualProvider.DefaultOpenAngleDegrees - ManualProvider.DefaultNearClosedAngleDegrees) * p,
        Progress: p,
        Velocity: 0.0,
        Confidence: 1.0,
        Source: LidStateSource.Manual);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Trace.Log("overlay: loaded");
        try
        {
            _capture = new DesktopCapture();
            _capture.Start();

            // Panel pixel size (2026-09-19 workaround, HC §6.2): the swapchain
            // must match the panel's PHYSICAL pixel size - after the Windows
            // update on this machine, larger composition swapchains are shown
            // unscaled (cropped) with broken alpha. ActualWidth is in DIPs;
            // RasterizationScale is the live monitor scale factor.
            double scale = CapturePanel.XamlRoot?.RasterizationScale ?? 1.0;
            int pw = Math.Max(1, (int)Math.Round(CapturePanel.ActualWidth * scale));
            int ph = Math.Max(1, (int)Math.Round(CapturePanel.ActualHeight * scale));
            Trace.Log($"overlay: panel pixel size {pw}×{ph} (scale {scale:0.00})");

            _renderer = new CaptureRenderer(CapturePanel, _capture, Clock);
            _renderer.Initialize(pw, ph);

            SizeInt32 size = _capture.Item.Size;
            OverlayCaption.Text =
                $"overlay · {_capture.MonitorDescription} · {size.Width}×{size.Height} · GPU→GPU";
            CaptureState = "running";
            Trace.Log("overlay: capture running");

            if (_selfTest)
            {
                _ = RunWarpSelfTestAsync();
            }
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
