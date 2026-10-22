using System;
using DuoFlow.Core;
using DuoFlow.Render;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    // M2.4 demo mode. Built lazily in OnLoaded: it needs the window's
    // DispatcherQueue, which is only meaningful once the window exists.
    private DemoPlayer? _demo;

    public MainWindow(OverlayWindow? overlay, bool smoke, bool lightSweep = false)
    {
        _overlay = overlay;
        _smoke = smoke;
        InitializeComponent();

        // M2.2 acceptance hook (--light-sweep): start with the sweep on so
        // warp-selftest can measure the band without a temporary source edit.
        // The PANEL stays the authority (DD-048) - this only seeds its initial
        // state, exactly like a user ticking the box.
        if (lightSweep)
        {
            SweepCheck.IsChecked = true;
        }

        // M1.2 wiring: StartAsync emits the baseline state (Progress = 0)
        // synchronously, which initializes the display below. Fire-and-forget
        // is safe - ManualProvider.StartAsync returns Task.CompletedTask.
        _manualProvider.StateChanged += OnManualStateChanged;
        _ = _manualProvider.StartAsync();

        // Type-inferred lambda on purpose: WindowClosedEventArgs is not
        // referenceable from C# in the WinAppSDK projection (CS0246, same
        // pitfall as OverlayWindow.Closed since M0.2).
        Closed += (_, _) => DetachManualProvider();
        // The console is the app's only visible surface with a close button:
        // closing it must take the (borderless, click-through, Alt+Tab-hidden)
        // overlay down with it, or the overlay lingers with no way to close
        // it except Task Manager (2026-09-19, real user hit).
        //
        // 2026-10-22: close the overlay FIRST so OverlayWindow.Cleanup() runs
        // and the capture session / D3D targets are released. Environment.Exit
        // stays as the hard backstop because WinUI 3 unpackaged
        // Application.Exit() only stops the DispatcherQueue (the process
        // lingered - verified 2026-09-19).
        Closed += (_, _) =>
        {
            Trace.Log("console: CLOSED event -> closing overlay + Environment.Exit(0)");
            try { _overlay?.Close(); } catch { /* already closed */ }
            Trace.Log("console: overlay closed, exiting process");
            Environment.Exit(0);
        };
        ((FrameworkElement)Content).Loaded += OnLoaded;
    }

    private void DetachManualProvider()
    {
        // Nothing may outlive the window: detach the handler and stop the
        // provider (it only flips its lifecycle flag - no async work).
        _demo?.Pause();
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
            // M1.2: grown from 470 to 620 to fit the Manual Progress block.
            // M2.5: grown again for the tuning panel - the height is taken
            // from the work area (capped) so it never runs off-screen on a
            // shorter display; the ScrollViewer covers the rest.
            //
            // WIDTH IS DELIBERATELY UNCHANGED at 580: overlay-p0-verify.ps1
            // parks its white reference window at 35% of the screen width
            // (x=672 on 1920) and this console is TOPMOST - widening it would
            // silently cover the reference and void the transparency test.
            RectInt32 work = DisplayArea.Primary.WorkArea;
            int consoleHeight = Math.Min(960, Math.Max(420, work.Height - 48));
            AppWindow.Resize(new SizeInt32(580, consoleHeight));
            int x = work.X + 24;
            int y = Math.Max(work.Y + 8, work.Y + work.Height - consoleHeight - 24);
            AppWindow.Move(new PointInt32(x, y));
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
            }
            Trace.Log($"console: positioned OK ({580}×{consoleHeight} at {x},{y})");
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

        // M2.4: the demo player drives ManualProvider - the same entry point
        // the slider uses (DD-002). It never touches the renderer or the
        // engine directly.
        _demo = new DemoPlayer(DispatcherQueue, p => _manualProvider.SetProgress(p));
        _demo.Ticked += UpdateDemoStatus;
        UpdateDemoStatus();
        Trace.Log("console: demo player ready");

        if (_smoke)
        {
            WriteSmoke(); // earliest possible report, even if ticks never fire
        }
    }

    private void Refresh()
    {
        _ticks++;

        // M2.5: push the panel's initial state once the renderer exists
        // (the two windows load in an unspecified order).
        if (!_tuningPushed)
        {
            ApplyTuning();
        }

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

        // M2.4: a real drag means the user took over - stop the demo so it
        // does not fight the mouse. (Demo-driven write-backs never reach this
        // line: they happen inside the _syncingSlider guard above.)
        _demo?.Pause();

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

    // ------------------------------------------------------------------
    // M2.4 demo mode
    // ------------------------------------------------------------------

    private void OnPlayOpening(object sender, RoutedEventArgs e) => PlayDemo(1.0);

    private void OnPlayClosing(object sender, RoutedEventArgs e) => PlayDemo(0.0);

    private void PlayDemo(double target)
    {
        if (_demo is null)
        {
            return;
        }

        _demo.DurationSeconds = DurationSlider.Value;
        _demo.Loop = LoopCheck.IsChecked == true;
        // Start from the provider's value, not from the slider's: the slider
        // can lag a write-back, the provider is the source of truth.
        _demo.Play(_manualProvider.CurrentState.Progress, target);
        Trace.Log($"demo: play {_manualProvider.CurrentState.Progress:0.00}->{target:0.00} " +
                  $"duration={_demo.DurationSeconds:0.0}s loop={_demo.Loop}");
        UpdateDemoStatus();
    }

    private void OnPauseDemo(object sender, RoutedEventArgs e)
    {
        _demo?.Pause();
        Trace.Log("demo: paused");
        UpdateDemoStatus();
    }

    private void OnLoopChanged(object sender, RoutedEventArgs e)
    {
        if (_demo is null)
        {
            return;
        }
        _demo.Loop = LoopCheck.IsChecked == true;
        Trace.Log($"demo: loop={_demo.Loop}");
        UpdateDemoStatus();
    }

    private void UpdateDemoStatus()
    {
        if (_demo is null)
        {
            return;
        }
        DemoStatus.Text = _demo.IsRunning
            ? $"运行中 · 单程 {_demo.DurationSeconds:0.0}s · 循环 {(_demo.Loop ? "开" : "关")} · " +
              $"当前 {_manualProvider.CurrentState.Progress:0.00}"
            : $"空闲 · 单程 {_demo.DurationSeconds:0.0}s · 循环 {(_demo.Loop ? "开" : "关")} · " +
              $"当前 {_manualProvider.CurrentState.Progress:0.00}";
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

    // ------------------------------------------------------------------
    // M2.5 render tuning panel.
    //
    // The panel is the EDITOR and the renderer holds the last ACCEPTED
    // snapshot. Every change builds a complete immutable WarpOptions /
    // BlurOptions pair and hands it to CaptureRenderer.SetOptions, which
    // validates all-or-nothing (DD-048). Nothing here touches
    // ManualProvider: progress is lid input, these are how that progress is
    // drawn.
    // ------------------------------------------------------------------

    // Guard for programmatic writes into the tuning widgets (reset / clamp
    // write-backs), so they do not re-enter ApplyTuning and fight each other.
    private bool _syncingTuning;

    // The console and the overlay load in an unspecified order, so the first
    // ApplyTuning can run before OverlayWindow has built its CaptureRenderer.
    // Rather than guess, the 1 s refresh retries until the options have been
    // pushed once - the widgets are the editor, so whatever they hold at that
    // moment is what the renderer should start with.
    private bool _tuningPushed;

    private void OnTuningValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        => ApplyTuning();

    private void OnTuningSelectionChanged(object sender, SelectionChangedEventArgs e)
        => ApplyTuning();

    // CheckBox.Checked/Unchecked are RoutedEventHandler, not
    // SelectionChangedEventHandler - WinUI's XAML compiler rejects the
    // selection handler outright (CS0123).
    private void OnTuningToggled(object sender, RoutedEventArgs e)
        => ApplyTuning();

    private void OnResetTuning(object sender, RoutedEventArgs e)
    {
        _syncingTuning = true;
        try
        {
            // Re-seed from the compiled-in defaults, never from a copy: if a
            // default changes in DuoFlow.Render, "restore defaults" follows.
            MaxFoldSlider.Value = WarpOptions.DefaultMaxFoldDegrees;
            CameraSlider.Value = WarpOptions.DefaultCameraDistanceRatio;
            MaxBlurSlider.Value = BlurOptions.DefaultMaxBlurPixels;
            EnvelopeStartSlider.Value = BlurOptions.DefaultStartProgress;
            EnvelopeFullSlider.Value = BlurOptions.DefaultFullProgress;
            TiltCombo.SelectedIndex = 0;    // away
            HingeCombo.SelectedIndex = 0;   // bottom
            GradientCheck.IsChecked = false;
            HueStartSlider.Value = GradientOptions.DefaultHueStartDegrees;
            HueEndSlider.Value = GradientOptions.DefaultHueEndDegrees;
            GradientStrengthSlider.Value = 0.0;
            SweepCheck.IsChecked = false;
            SweepPositionSlider.Value = LightSweepOptions.DefaultPosition;
            SweepWidthSlider.Value = LightSweepOptions.DefaultWidth;
            SweepStrengthSlider.Value = LightSweepOptions.DefaultStrength;
            SweepDirectionCombo.SelectedIndex = 0;   // hinge -> far
        }
        finally
        {
            _syncingTuning = false;
        }
        ApplyTuning();
    }

    private void ApplyTuning()
    {
        if (_syncingTuning || _overlay?.Renderer is not { } renderer)
        {
            return;   // renderer not built yet - Refresh() retries
        }

        // Keep the envelope coherent BEFORE building the options: a start at
        // or past the full point is a UI state, not a render state - pushing
        // it through would only get rejected and show an error for something
        // the user did not mean. The write-back is guarded.
        double start = EnvelopeStartSlider.Value;
        double full = EnvelopeFullSlider.Value;
        if (full <= start)
        {
            full = Math.Min(1.0, start + 0.01);
            _syncingTuning = true;
            try { EnvelopeFullSlider.Value = full; }
            finally { _syncingTuning = false; }
        }

        var warp = new WarpOptions
        {
            MaxFoldDegrees = MaxFoldSlider.Value,
            CameraDistanceRatio = CameraSlider.Value,
            TiltAwayFromViewer = TiltCombo.SelectedIndex == 0,
            HingeAtTop = HingeCombo.SelectedIndex == 1,
        };
        var blur = new BlurOptions
        {
            MaxBlurPixels = MaxBlurSlider.Value,
            StartProgress = start,
            FullProgress = full,
        };
        var gradient = new GradientOptions
        {
            Enabled = GradientCheck.IsChecked == true,
            HueStartDegrees = HueStartSlider.Value,
            HueEndDegrees = HueEndSlider.Value,
            Strength = GradientStrengthSlider.Value,
        };
        var sweep = new LightSweepOptions
        {
            Enabled = SweepCheck.IsChecked == true,
            Position = SweepPositionSlider.Value,
            Width = SweepWidthSlider.Value,
            Strength = SweepStrengthSlider.Value,
            Direction = SweepDirectionCombo.SelectedIndex == 1
                ? SweepDirection.FarToHinge
                : SweepDirection.HingeToFar,
        };

        string? error = renderer.SetOptions(warp, blur, gradient, sweep);
        if (error is null)
        {
            _tuningPushed = true;
        }
        TuningStatus.Text = error is null
            ? $"生效：MaxFold {warp.MaxFoldDegrees:0}° · r {warp.CameraDistanceRatio:0.00} · " +
              $"{(warp.TiltAwayFromViewer ? "away" : "toward")} · 铰链{(warp.HingeAtTop ? "顶" : "底")} · " +
              $"maxBlur {blur.MaxBlurPixels:0}px · 包络 {blur.StartProgress:0.00}→{blur.FullProgress:0.00} · " +
              $"渐变 {(gradient.Enabled ? $"开 {gradient.Strength:0.00}" : "关")} · " +
              $"光带 {(sweep.Enabled ? $"开 {sweep.Strength:0.00} w{sweep.Width:0.00}" : "关")}"
            : $"✗ 已拒绝（保持上一组生效值）：{error}";
    }

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
        $"捕获·渲染（迁入 overlay）  {r.CaptureState} · warp={r.WarpState} · {_lastFps:0} FPS · {r.RenderFrameMs:0.0}ms · 画质 {r.QualityScale:0.00} · 累计 {r.CaptureFrames} 帧（GPU→GPU→Warp）";
}
