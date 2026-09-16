namespace DuoFlow.Core;

/// <summary>
/// M1.3 Animation Engine: the middle layer between raw provider input
/// (ManualProvider today, Camera/Sensor later) and whatever consumes motion
/// (M1.4 render pipeline). It turns DISCONTINUOUS raw Progress into a
/// CONTINUOUS smoothed Progress with a real Velocity, enforcing DD-002
/// downstream discipline (consumers only ever see progress in 0~1; the
/// engine itself contains no rendering concept).
/// </summary>
/// <remarks>
/// Semantics fixed for M1.3 (DD-038):
///  - Rate-limited exponential approach: desired slew rate toward the target
///    is (target − current) / τ, hard-capped at MaxProgressPerSecond — the
///    "Progress must never jump" guarantee (keyboard Home/End included).
///  - Arrival: within ArrivalEpsilon of the target (or crossing it) the
///    velocity is forced to 0 — no asymptotic crawl, no overshoot chatter.
///  - Time is injected (<see cref="ITimeSource"/>); a single integration step
///    is clamped to [0, MaxDeltaTimeSeconds] (negative dt = clock anomaly,
///    huge dt = paused process / stalled loop). The rate cap bounds motion
///    regardless.
///  - First <see cref="Update"/> SNAPS to the target: a freshly created
///    engine adopts the world as it is; anti-jump only applies to changes it
///    has observed.
///  - NaN raw progress is IGNORED (previous target kept) — providers clamp,
///    but the engine defends itself.
///  - Output <see cref="LidState"/>: Progress/Velocity are the smoothed
///    values (Velocity is in progress units per second, finally a real
///    number instead of M1.1's constant 0); Angle is REBUILT from the
///    smoothed progress through the PROJECT_SPEC §6 illustrative mapping so
///    displays stay continuous; Source/Confidence pass through from the raw
///    input unchanged.
///  - Not thread-safe by design (M1: UI/render thread usage); the M4
///    Provider Manager owns serialization.
/// </remarks>
public sealed class AnimationEngine
{
    private readonly ITimeSource _time;
    private readonly AnimationEngineOptions _opt;

    private LidState _lastRaw;
    private long _lastTicks;
    private bool _hasTime;
    private double _current;   // smoothed progress, always within 0~1
    private double _velocity;  // signed slew rate, progress units / second

    public AnimationEngine(ITimeSource? timeSource = null, AnimationEngineOptions? options = null)
    {
        _time = timeSource ?? new StopwatchTimeSource();
        _opt = options ?? new AnimationEngineOptions();
        _lastRaw = new LidState(
            Angle: ManualProvider.DefaultOpenAngleDegrees,
            Progress: 0.0,
            Velocity: 0.0,
            Confidence: 1.0,
            Source: LidStateSource.Unknown);
    }

    /// <summary>Smoothed progress (0~1). Pulled, never cached in consumers.</summary>
    public double CurrentProgress => _current;

    /// <summary>Current signed slew rate (progress units / second).</summary>
    public double CurrentVelocity => _velocity;

    /// <summary>The state a render pass should consume right now.</summary>
    public LidState Current => CreateState();

    /// <summary>
    /// Feed a raw provider state (call from StateChanged). Advances the
    /// simulation to "now" with the new target and returns the smoothed state.
    /// </summary>
    public LidState Update(LidState raw)
    {
        if (raw.Progress is double.NaN)
        {
            // Providers clamp and reject NaN, but the engine defends itself:
            // ignore this frame's progress, keep Source/Confidence metadata.
            _lastRaw = _lastRaw with { Source = raw.Source, Confidence = raw.Confidence };
        }
        else
        {
            _lastRaw = raw;
        }

        StepTo(_time.GetTimestampTicks());
        return Current;
    }

    /// <summary>
    /// Advance the simulation with no new input (render-frame drive). This is
    /// what keeps the motion continuous after a keyboard jump: the raw target
    /// stays the same while <see cref="Tick"/> slews toward it frame by frame.
    /// </summary>
    public LidState Tick()
    {
        StepTo(_time.GetTimestampTicks());
        return Current;
    }

    private void StepTo(long nowTicks)
    {
        double target = Math.Clamp(_lastRaw.Progress, 0.0, 1.0);

        if (!_hasTime)
        {
            // First observation: adopt the world as it is (see remarks).
            _hasTime = true;
            _lastTicks = nowTicks;
            _current = target;
            _velocity = 0.0;
            return;
        }

        double dt = (nowTicks - _lastTicks) / (double)TimeSpan.TicksPerSecond;
        _lastTicks = nowTicks;
        if (dt < 0.0)
        {
            dt = 0.0; // clock anomaly: hold still, never move backwards
        }
        else if (dt > _opt.MaxDeltaTimeSeconds)
        {
            dt = _opt.MaxDeltaTimeSeconds; // pause/hang guard (rate cap still applies)
        }

        double error = target - _current;
        if (Math.Abs(error) <= _opt.ArrivalEpsilon)
        {
            _current = target;
            _velocity = 0.0;
            return;
        }

        double maxV = Math.Max(_opt.MaxProgressPerSecond, 1e-9);
        double tau = Math.Max(_opt.SmoothingTauSeconds, 1e-3);
        double desiredV = error / tau;
        double v = Math.Clamp(desiredV, -maxV, maxV);

        double next = _current + v * dt;
        if ((v > 0 && next >= target) || (v < 0 && next <= target))
        {
            // Arrived this step: snap, zero the velocity, stop chasing.
            _current = target;
            _velocity = 0.0;
            return;
        }

        _current = Math.Clamp(next, 0.0, 1.0);
        _velocity = v;
    }

    private LidState CreateState() => new(
        Angle: ManualProvider.DefaultOpenAngleDegrees
               - (ManualProvider.DefaultOpenAngleDegrees - ManualProvider.DefaultNearClosedAngleDegrees) * _current,
        Progress: _current,
        Velocity: _velocity,
        Confidence: _lastRaw.Confidence,
        Source: _lastRaw.Source);
}
