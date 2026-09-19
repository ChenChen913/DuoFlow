using System;

namespace DuoFlow.Render;

/// <summary>
/// M1.8 model-v2 frosted blur knobs (DD-046, adopted from the DuoFold
/// reference). The blur radius follows the physical gap height with a
/// quadratic ease-in: radius = saturate(normGap² × 1.5 × intensity) ×
/// MaxBlurPixels, and the light falloff follows the radius - one physical
/// variable drives the whole shadow transition (the separate hinge mask,
/// blur envelope input and dimmer of DD-040..DD-042 are superseded).
/// </summary>
public sealed class BlurOptions
{
    public const double DefaultMaxBlurPixels = 160.0;
    public const double DefaultStartProgress = 0.30;
    public const double DefaultFullProgress = 0.60;

    /// <summary>Maximum frosted radius in DEVICE pixels of the render target,
    /// reached once the intensity envelope saturates (the reference ships
    /// 160 with mip pre-filtering making it velvet-smooth). Validated to
    /// (0, 400].</summary>
    public double MaxBlurPixels { get; init; } = DefaultMaxBlurPixels;

    /// <summary>Progress at which the effect envelope starts rising
    /// (2026-09-19 real-user tuning: visible 虚化 from ~0.35). Below this
    /// progress the frost and shadow contribute nothing.</summary>
    public double StartProgress { get; init; } = DefaultStartProgress;

    /// <summary>Progress at which the envelope saturates at 1. Linear ramp
    /// between Start and Full.</summary>
    public double FullProgress { get; init; } = DefaultFullProgress;

    /// <summary>Effect intensity envelope: 0 before StartProgress, linear
    /// ramp to 1 at FullProgress, then 1. Applied by the wiring BEFORE the
    /// intensity reaches the shader.</summary>
    public double IntensityAt(double progress)
    {
        if (double.IsNaN(progress) || double.IsInfinity(progress))
        {
            throw new ArgumentOutOfRangeException(nameof(progress), progress,
                "Progress must be finite.");
        }
        double p = Math.Clamp(progress, 0.0, 1.0);
        double t = (p - StartProgress) / (FullProgress - StartProgress);
        return Math.Clamp(t, 0.0, 1.0);
    }

    /// <summary>Validates the knobs. Throws on anything that would render
    /// garbage.</summary>
    public void Validate()
    {
        if (double.IsNaN(MaxBlurPixels) || double.IsInfinity(MaxBlurPixels)
            || MaxBlurPixels <= 0.0 || MaxBlurPixels > 400.0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBlurPixels), MaxBlurPixels,
                "MaxBlurPixels must be within (0, 400] device pixels.");
        }
        if (double.IsNaN(StartProgress) || StartProgress < 0.0 || StartProgress >= 0.9)
        {
            throw new ArgumentOutOfRangeException(nameof(StartProgress), StartProgress,
                "StartProgress must be within [0, 0.9).");
        }
        if (double.IsNaN(FullProgress) || FullProgress <= StartProgress || FullProgress > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(FullProgress), FullProgress,
                "FullProgress must be within (StartProgress, 1].");
        }
    }
}
