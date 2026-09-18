using System;
using DuoFlow.Core;

namespace DuoFlow.App;

/// <summary>
/// M1.4 wiring: thread-safe facade over the M1.3 AnimationEngine.
/// <see cref="Update"/> is called from the UI thread (the console window
/// forwards ManualProvider.StateChanged), <see cref="Tick"/> from the
/// free-threaded capture frame-pool worker inside the render callback.
/// The engine itself stays single-threaded by design (DD-038); serialization
/// is the wiring's job until the M4 Provider Manager owns it. The engine's
/// semantics are untouched - DD-002/DD-038 discipline: the render side only
/// ever sees smoothed output, never the raw provider value.
/// </summary>
public sealed class LidAnimationClock
{
    private readonly object _gate = new();
    private readonly AnimationEngine _engine;

    public LidAnimationClock(AnimationEngine? engine = null)
        => _engine = engine ?? new AnimationEngine();

    /// <summary>Feed a raw provider state (UI thread). Advances the
    /// simulation to now with the new target (DD-038 dual entry).</summary>
    public LidState Update(LidState raw)
    {
        lock (_gate)
        {
            return _engine.Update(raw);
        }
    }

    /// <summary>Advance with no new input (render frame, worker thread).</summary>
    public LidState Tick()
    {
        lock (_gate)
        {
            return _engine.Tick();
        }
    }

    /// <summary>The smoothed state right now (pull-based, DD-038).</summary>
    public LidState Current
    {
        get
        {
            lock (_gate)
            {
                return _engine.Current;
            }
        }
    }

    /// <summary>Smoothed progress 0~1 for display (pull-based).</summary>
    public double CurrentProgress
    {
        get
        {
            lock (_gate)
            {
                return _engine.CurrentProgress;
            }
        }
    }

    /// <summary>Smoothed slew rate (progress/second) for diagnostics.</summary>
    public double CurrentVelocity
    {
        get
        {
            lock (_gate)
            {
                return _engine.CurrentVelocity;
            }
        }
    }
}
