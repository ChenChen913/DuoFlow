using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace DuoFlow.App;

/// <summary>
/// M0.3 control console: lives bottom-left, always on top of the overlay,
/// and live-renders the verification matrix (fullscreen / topmost /
/// click-through / no-activate / tool-window / DWM transparency /
/// multi-monitor enum / capture FPS). In smoke mode it writes
/// duoflow-overlay-smoke.json — once immediately after load, then refreshed
/// every second — so the CI gate reads the freshest report possible.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly OverlayWindow? _overlay;
    private readonly bool _smoke;

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
        ((FrameworkElement)Content).Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Trace.Log("console: loaded");

        try
        {
            // Bottom-left control panel, always on top of the overlay.
            RectInt32 work = DisplayArea.Primary.WorkArea;
            AppWindow.Resize(new SizeInt32(580, 470));
            int x = work.X + 24;
            int y = Math.Max(work.Y + work.Height - 494, work.Y + 8);
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
        $"捕获·渲染（迁入 overlay）  {r.CaptureState} · {_lastFps:0} FPS · 累计 {r.CaptureFrames} 帧（GPU→GPU）";
}
