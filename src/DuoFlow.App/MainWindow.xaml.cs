using System;
using DuoFlow.Capture;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace DuoFlow.App;

/// <summary>
/// M0.2 verification window: captures the primary desktop and shows it live
/// inside a SwapChainPanel, proving the Desktop -> GPU texture -> panel chain.
/// </summary>
public sealed partial class MainWindow : Window
{
    private DesktopCapture? _capture;
    private CaptureRenderer? _renderer;
    private DispatcherQueueTimer? _fpsTimer;
    private long _lastFrameCount;
    private int _started;
    private int _ticks;

    public MainWindow()
    {
        InitializeComponent();
        ((FrameworkElement)Content).Loaded += OnLoaded;
        Closed += (sender, args) => Cleanup();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        try
        {
            _capture = new DesktopCapture();
            _capture.Start();

            _renderer = new CaptureRenderer(CapturePanel, _capture);
            _renderer.Initialize();

            SizeInt32 size = _capture.Item.Size;
            UpdateStatus(
                $"捕获中：{_capture.MonitorDescription} · {size.Width}×{size.Height} · " +
                $"GPU texture · {_capture.DriverInfo} · 0 FPS");

            _fpsTimer = DispatcherQueue.CreateTimer();
            _fpsTimer.Interval = TimeSpan.FromSeconds(1);
            _fpsTimer.Tick += (_, _) =>
            {
                long now = _renderer.PresentedFrames;
                long fps = now - _lastFrameCount;
                _lastFrameCount = now;
                string noFrameHint = (now == 0 && _ticks >= 5)
                    ? (_capture.LastError is not null
                        ? $" · ⚠ 帧回调异常：{_capture.LastError}"
                        : " · ⚠ 会话已启动但无帧送达（云端虚拟显卡限制，真机待验证）")
                    : "";
                _ticks++;
                UpdateStatus(
                    $"捕获中：{_capture.MonitorDescription} · {size.Width}×{size.Height} · " +
                    $"GPU texture · {_capture.DriverInfo} · {fps} FPS · 已捕获 {now} 帧{noFrameHint}");
            };
            _fpsTimer.Start();
        }
        catch (Exception ex)
        {
            // Never crash the demo: surface the failure in the status bar so the
            // CI screenshot still documents what happened.
            StatusText.Text = $"捕获失败：{ex.GetType().Name}: {ex.Message}";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.OrangeRed);
        }
    }

    private void UpdateStatus(string message)
    {
        DispatcherQueue.TryEnqueue(() => StatusText.Text = message);
    }

    private void Cleanup()
    {
        _fpsTimer?.Stop();
        _renderer?.Dispose();
        _capture?.Dispose();
        _renderer = null;
        _capture = null;
    }
}
