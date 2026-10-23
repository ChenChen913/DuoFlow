namespace DuoFlow.Vision;

/// <summary>
/// Throttles a camera stream down to the rate the rest of the pipeline wants.
///
/// <para>
/// Why this exists at all: a camera delivers 30 or 60 frames per second because
/// that is what a video stream is, but angle estimation does not need more than
/// ~10 Hz - the lid cannot move faster than that, and every extra frame is
/// vision work (and battery) spent to compute the same answer twice. Dropping
/// frames is therefore the CHEAPEST performance decision in the whole camera
/// path, which is why it lives in a pure class with a fake clock instead of
/// inside the WinRT callback.
/// </para>
/// <para>
/// Why the slack: a 10.0 fps camera and a 10.0 fps target produce intervals of
/// exactly 100 ms, but the timestamps jitter by a few ms. Testing
/// <c>elapsed &gt;= 1/fps</c> would then drop every other frame and silently
/// halve the rate to 5 fps. Accepting at <c>interval * 0.9</c> tolerates the
/// jitter while still dropping a 30 fps stream down to 10.
/// </para>
/// <para>
/// Not thread-safe by design: it is called from one frame callback.
/// </para>
/// </summary>
public sealed class FrameRateGovernor
{
    /// <summary>Fraction of the target interval that already counts as "due".
    /// 0.9 = tolerate 10% early arrival; see the class remarks.</summary>
    public const double DefaultSlack = 0.9;

    private readonly double _slack;
    private double _targetFps;
    private double _lastAcceptedSeconds = double.NegativeInfinity;
    private long _accepted;
    private long _dropped;

    public FrameRateGovernor(double targetFps, double slack = DefaultSlack)
    {
        if (!double.IsFinite(targetFps) || targetFps < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetFps), $"target fps must be finite and >= 0, got {targetFps}");
        }
        if (!double.IsFinite(slack) || slack <= 0.0 || slack > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(slack), $"slack must be in (0,1], got {slack}");
        }

        _targetFps = targetFps;
        _slack = slack;
    }

    /// <summary>Target frames per second. 0 means "no throttling" (accept all).</summary>
    public double TargetFps
    {
        get => _targetFps;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"target fps must be finite and >= 0, got {value}");
            }

            // Nothing to reset: the minimum interval is derived from the
            // CURRENT target on every Accept call, and the last acceptance
            // stays where it was. Lowering 10 -> 1 fps therefore holds the
            // next frame back for 0.9 s (correct - slower means less often),
            // while raising 1 -> 10 lets it through after 0.1 s. Resetting the
            // clock here would instead let one frame through immediately on
            // ANY change, including a slowdown, which is the opposite of what
            // a slowdown means.
            _targetFps = value;
        }
    }

    public long Accepted => _accepted;

    public long Dropped => _dropped;

    /// <summary>Fraction of offered frames that were dropped, 0 when none were offered.</summary>
    public double DropRatio
    {
        get
        {
            long total = _accepted + _dropped;
            return total == 0 ? 0.0 : (double)_dropped / total;
        }
    }

    /// <summary>
    /// Offer a frame timestamp (seconds, monotonic) and get back whether the
    /// pipeline should process it. Dropped frames are counted, not queued.
    /// </summary>
    public bool Accept(double nowSeconds)
    {
        if (!double.IsFinite(nowSeconds))
        {
            return false;
        }

        if (_targetFps <= 0.0)
        {
            _accepted++;
            _lastAcceptedSeconds = nowSeconds;
            return true;
        }

        double minInterval = (1.0 / _targetFps) * _slack;
        if (nowSeconds - _lastAcceptedSeconds < minInterval)
        {
            _dropped++;
            return false;
        }

        _accepted++;
        _lastAcceptedSeconds = nowSeconds;
        return true;
    }

    /// <summary>Forget the last acceptance (e.g. after a stream restart), keep the counters.</summary>
    public void ResetClock() => _lastAcceptedSeconds = double.NegativeInfinity;
}
