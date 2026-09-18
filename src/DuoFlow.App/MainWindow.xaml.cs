using System;
using DuoFlow.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace DuoFlow.App;

/// <summary>
/// M0.3 control console: lives bottom-left, always on top of the overlay,
/// and live-renders the verification matrix (fullscreen / topmost /
/// click-through / no-activate / tool-window / DWM transparency /
/// multi-monitor enum / capture FPS). In smoke mode it writes
/// duoflow-overlay-smoke.json — once immediately after load, then refreshed
/// every second — so the CI gate reads the freshest report possible.
/// M1.2: the same window also hosts the Manual Progress slider that drives
/// ManualProvider (Slider → SetProgress → StateChanged → display). The
/// slider value never reaches the render layer directly (DD-002).
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly OverlayWindow? _overlay;
    private readonly bool _smoke;

    // M1.2: the provider - not the slider - is the single source of truth.
    // Every displayed value is pulled from ManualProvider.CurrentState;
    // the UI keeps no copy of the lid state.
    private readonly ManualProvider _manualProvider = new();

    // Loop guard for Slider.Value write-backs: SetProgress → StateChanged →
    // slider write would re-enter ValueChanged → SetProgress and spin.
    // Write-backs happen only inside this flag and only when the value
    // really differs (NeedsSliderSync).
    private bool _syncingSlider;

    private DispatcherQueueTimer? _timer;
    private long _lastFrames;
    private double _lastFps;
    private int _ticks;
    private OverlayReport? _lastReport;

    public MainWindow(OverlayWindow? overlay, bool smoke)
    {
        _overlay = overlay;
        _smoke = smoke;
        InitializeComponent();

        // M1.2 wiring: StartAsync emits the baseline state (Progress = 0)
        // synchronously, which initializes the display below. Fire-and-forget
        // is safe - ManualProvider.StartAsync returns Task.CompletedTask.
        _manualProvider.StateChanged += OnManualStateChanged;
        _ = _manualProvider.StartAsync();

        // Type-inferred lambda on purpose: WindowClosedEventArgs is not
        // referenceable from C# in the WinAppSDK projection (CS0246, same
        // pitfall as OverlayWindow.Closed since M0.2).
        Closed += (_, _) => DetachManualProvider();
        ((FrameworkElement)Content).Loaded += OnLoaded;
    }

    private void DetachManualProvider()
    {
        // Nothing may outlive the window: detach the handler and stop the
        // provider (it only flips its lifecycle flag - no async work).
        _manualProvider.StateChanged -= OnManualStateChanged;
        _ = _manualProvider.StopAsync();
        Trace.Log("console: manual provider detached + stopped");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Trace.Log("console: loaded");

        try
        {
            // Bottom-left control panel, always on top of the overlay.
            // (M1.2: grown from 470 to 620 to fit the Manual Progress block.)
            RectInt32 work = DisplayArea.Primary.WorkArea;
            AppWindow.Resize(new SizeInt32(580, 620));
            int x = work.X + 24;
            int y = Math.Max(work.Y + work.Height - 644, work.Y + 8);
            AppWindow.Move(new PointInt32(x, y));
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
            }
            Trace.Log("console: positioned OK");
        }
        catch (Exception ex)
        {
            Trace.Log($"console: positioning FAILED: {ex.Message}");
            Report.Text = $"[console] window positioning failed: {ex.Message}";
        }

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Trace.Log("console: timer started");

        if (_smoke)
        {
            WriteSmoke(); // earliest possible report, even if ticks never fire
        }
    }

    private void Refresh()
    {
        _ticks++;

        OverlayReport report;
        try
        {
            report = OverlayProbe.Collect(_overlay);
        }
        catch (Exception ex)
        {
            Trace.Log($"console: probe FAILED at tick {_ticks}: {ex.GetType().Name}: {ex.Message}");
            Report.Text = $"[probe] 收集失败：{ex.GetType().Name}: {ex.Message}";
            return;
        }

        _lastReport = report;
        _lastFps = report.CaptureFrames - _lastFrames;
        _lastFrames = report.CaptureFrames;
        report.CaptureFps = _lastFps;

        Report.Text = Format(report);
        Trace.Log($"console: tick {_ticks} frames={report.CaptureFrames} fps={_lastFps:0}");

        if (_smoke)
        {
            WriteSmoke();
        }
    }

    private void WriteSmoke()
    {
        try
        {
            OverlayReport snapshot = _lastReport ?? OverlayProbe.Collect(_overlay);
            OverlayProbe.WriteSmokeJson(snapshot);
            Trace.Log("console: smoke json written");
        }
        catch (Exception ex)
        {
            Trace.Log($"console: smoke write FAILED: {ex.Message}");
        }
    }

    private static string Mark(bool ok) => ok ? "✓ 通过" : "✗ 未通过";

    // ------------------------------------------------------------------
    // M1.2: Manual Progress driving chain.
    //
    //  Slider.ValueChanged ─────────────┐
    //                                   ├─> ManualProvider.SetProgress(v)
    //  KeyboardAccelerator Home/End ────┘            │
    //                                                ▼
    //                                StateChanged → UpdateProgressDisplay()
    //                                                │
    //                      display ← CurrentState ───┘  (+ slider sync,
    //                      (single source of truth)      guarded write-back)
    // ------------------------------------------------------------------

    private void OnProgressSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // Echo of a guarded display sync: the provider already knows this
        // value - do not drive it again from its own StateChanged.
        if (_syncingSlider)
        {
            return;
        }

        _manualProvider.SetProgress(e.NewValue);
    }

    // Home → 0 / End → 1. Invoked with global scope regardless of which
    // element holds focus (Window has no KeyDown event in WinUI 3; the
    // accelerators live on the root ScrollViewer - see MainWindow.xaml).
    private void OnHomeAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        DriveProgress(0.0);   // fully open
    }

    private void OnEndAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        DriveProgress(1.0);   // NEAR fully closed (§6)
    }

    private void DriveProgress(double progress)
    {
        // Keyboard path: move the slider visual first (its echo is
        // swallowed by the guard), then drive the provider exactly once.
        // When the slider already shows the target (Home pressed twice),
        // ValueChanged never fires - SetProgress below still runs, keeping
        // DD-035's "same value re-raises" semantics.
        _syncingSlider = true;
        try
        {
            ProgressSlider.Value = progress;
        }
        finally
        {
            _syncingSlider = false;
        }

        _manualProvider.SetProgress(progress);
    }

    private void OnManualStateChanged(object? sender, LidState state)
    {
        // M1.4: the render side consumes the state through the AnimationEngine
        // (the clock lives on the overlay), never through the slider (DD-002 /
        // DD-038). ManualProvider semantics are untouched (DD-035).
        _overlay?.NotifyLidState(state);

        UpdateProgressDisplay();
    }

    private void UpdateProgressDisplay()
    {
        // Single source of truth: pull the state from the provider instead
        // of caching it in the UI (the event arg equals CurrentState here;
        // pulling keeps that true even if handler ordering ever changes).
        LidState current = _manualProvider.CurrentState;
        ProgressText.Text = FormatProgress(current.Progress, current.Angle);

        // M1.4: show what the render side actually consumes - the engine's
        // smoothed progress, pulled from the clock (never a cached copy).
        SmoothText.Text = _overlay is null
            ? "平滑 Progress 不可用（无 overlay）"
            : $"平滑 Progress（M1.3→M1.4 渲染输入）{_overlay.Clock.CurrentProgress:0.00}";

        // Follow the provider if it moved on its own (e.g. keyboard jump;
        // later: M1.3 smoothing). The guard + value check prevent loops.
        if (NeedsSliderSync(ProgressSlider.Value, current.Progress))
        {
            _syncingSlider = true;
            try
            {
                ProgressSlider.Value = current.Progress;
            }
            finally
            {
                _syncingSlider = false;
            }
        }
    }

    // Slider snaps Value to StepFrequency (0.01); compare with an epsilon
    // so float noise never triggers a pointless write-back.
    private static bool NeedsSliderSync(double sliderValue, double progress)
        => Math.Abs(sliderValue - progress) > 0.0005;

    private static string FormatProgress(double progress, double angle)
        => $"Progress {progress:0.00} · Angle ≈ {angle:0.0}°（§6 示意映射）";

    private string Format(OverlayReport r) =>
        $"Overlay 创建            {Mark(r.OverlayCreated)}\n" +
        $"全屏覆盖                {Mark(r.Fullscreen)}    窗口 {r.WindowRect} / 屏幕 {r.ScreenRect}\n" +
        $"Topmost 置顶            {Mark(r.Topmost)}    WS_EX_TOPMOST\n" +
        $"Click Through 穿透      {Mark(r.ClickThrough)}    WS_EX_TRANSPARENT（真实鼠标穿透待真机）\n" +
        $"No Activate 不抢焦点    {Mark(r.NoActivate)}    WS_EX_NOACTIVATE\n" +
        $"Alt+Tab 隐藏            {Mark(r.ToolWindow)}    WS_EX_TOOLWINDOW\n" +
        $"DWM 透明                {Mark(r.TransparentDwm)}\n" +
        $"多显示器枚举            {r.MonitorCount} 台    {r.Monitors}\n" +
        $"─────────────────────────────────────\n" +
        $"捕获·渲染（迁入 overlay）  {r.CaptureState} · warp={r.WarpState} · {_lastFps:0} FPS · 累计 {r.CaptureFrames} 帧（GPU→GPU→Warp）";
}
