namespace DuoFlow.Core;

/// <summary>
/// Tuning parameters of the M1.3 Animation Engine. Defaults chosen so the
/// full 0→1 travel never takes longer than ~0.75 s (rate limit) and settles
/// exponentially (τ) instead of oscillating; M2.3 Visual Tuning may adjust.
/// </summary>
public sealed record AnimationEngineOptions
{
    /// <summary>
    /// Exponential smoothing time constant τ in seconds: the slew rate toward
    /// the target is (target − current) / τ, capped by
    /// <see cref="MaxProgressPerSecond"/>. Larger = smoother but laggier.
    /// </summary>
    public double SmoothingTauSeconds { get; init; } = 0.10;

    /// <summary>
    /// Anti-jump rate cap in progress units per second (1.0 == full 0→1 travel
    /// in 1 s). This is the hard "Progress must never jump" guarantee: even a
    /// keyboard 0→1 discontinuity slews at most this fast.
    /// </summary>
    public double MaxProgressPerSecond { get; init; } = 2.0;

    /// <summary>
    /// Upper bound for one integration step, in seconds. Guards against
    /// paused/resumed processes or a stalled render loop "teleporting" the
    /// smoothed value after a long gap: time is clamped, then the rate cap
    /// bounds the motion anyway.
    /// </summary>
    public double MaxDeltaTimeSeconds { get; init; } = 0.25;

    /// <summary>Progress values within this distance of the target count as
    /// arrived (velocity forced to 0, no infinite asymptotic crawl).</summary>
    public double ArrivalEpsilon { get; init; } = 1e-6;
}
