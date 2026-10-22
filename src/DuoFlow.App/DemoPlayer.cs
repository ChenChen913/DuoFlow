using System;
using System.Diagnostics;
using Microsoft.UI.Dispatching;

namespace DuoFlow.App;

/// <summary>
/// M2.4 demo player: walks the lid progress between two endpoints on a 30 ms
/// clock so the whole effect can be watched hands-free (and so M2.3 tuning can
/// be judged on a moving picture instead of one frozen progress value).
///
/// It drives <c>ManualProvider.SetProgress</c> - the SAME entry point the
/// slider uses - so the demo cannot bypass the input chain (DD-002):
/// provider → AnimationEngine → render. Nothing here touches the renderer,
/// and the engine still does the smoothing (DD-038): the demo emits a step
/// function, the engine decides what the screen shows.
///
/// Timing uses a Stopwatch, not the tick count: a DispatcherQueueTimer drifts
/// under load, and a demo that silently plays slower than its stated duration
/// would make M2.3's "快/慢" checks meaningless.
/// </summary>
public sealed class DemoPlayer
{
    private const int TickMs = 30;

    private readonly Action<double> _setProgress;
    private readonly DispatcherQueueTimer _timer;
    private readonly Stopwatch _watch = new();

    private double _from;
    private double _to;
    private double _durationSeconds = 1.5;
    private double _elapsed;
    private TimeSpan _lastTick;

    public DemoPlayer(DispatcherQueue queue, Action<double> setProgress)
    {
        _setProgress = setProgress ?? throw new ArgumentNullException(nameof(setProgress));
        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(TickMs);
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>true while the clock is advancing.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Loop back and forth instead of stopping at the endpoint.
    /// Read on every tick, so it can be toggled mid-run.</summary>
    public bool Loop { get; set; }

    /// <summary>Seconds for one full 0→1 (or 1→0) pass. Clamped to [0.2, 30]
    /// so a bad value cannot produce a divide-by-zero or a stall.</summary>
    public double DurationSeconds
    {
        get => _durationSeconds;
        set => _durationSeconds = Math.Clamp(value, 0.2, 30.0);
    }

    /// <summary>Raised after each tick so the console can refresh its own
    /// readout (the provider already raised its own StateChanged).</summary>
    public event Action? Ticked;

    /// <summary>
    /// Start (or resume) a pass from the CURRENT provider value to
    /// <paramref name="target"/>. The remaining time is scaled to the
    /// remaining distance so the SPEED stays constant - resuming after a
    /// pause should not make the last third crawl.
    /// </summary>
    public void Play(double current, double target)
    {
        _from = Math.Clamp(current, 0.0, 1.0);
        _to = Math.Clamp(target, 0.0, 1.0);
        double distance = Math.Abs(_to - _from);

        if (distance < 1e-6)
        {
            // Already there: emit the endpoint once and do not spin the timer.
            _setProgress(_to);
            Pause();
            return;
        }

        _elapsed = 0.0;
        _watch.Restart();
        _lastTick = TimeSpan.Zero;
        IsRunning = true;
        _timer.Start();
    }

    /// <summary>Stop advancing, keep the current value (and the ability to
    /// resume with Play).</summary>
    public void Pause()
    {
        IsRunning = false;
        _timer.Stop();
        _watch.Stop();
    }

    private void Tick()
    {
        TimeSpan now = _watch.Elapsed;
        double dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (dt <= 0.0)
        {
            return;
        }

        _elapsed += dt;
        double distance = Math.Abs(_to - _from);
        // Time for THIS pass is proportional to the distance actually
        // travelled, so a partial resume keeps the same px/second.
        double passSeconds = Math.Max(0.05, _durationSeconds * distance);
        double t = _elapsed / passSeconds;

        if (t >= 1.0)
        {
            _setProgress(_to);
            if (Loop)
            {
                // Swap and carry the overshoot: the endpoint is emitted
                // exactly once and the speed does not hiccup at the turn.
                (_from, _to) = (_to, _from);
                _elapsed -= passSeconds;
                if (_elapsed < 0.0) { _elapsed = 0.0; }
            }
            else
            {
                Pause();
            }
        }
        else
        {
            _setProgress(_from + (_to - _from) * t);
        }

        Ticked?.Invoke();
    }
}
