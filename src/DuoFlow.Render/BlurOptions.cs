using System;

namespace DuoFlow.Render;

/// <summary>
/// M1.6 Blur knobs (DD-041). PROJECT_SPEC §12: blur = hingeMask × progress ×
/// maxBlur - strongest at the hinge, zero when fully open, never the whole
/// screen at once (the mask enforces locality), always smooth.
/// </summary>
public sealed class BlurOptions
{
    public const double DefaultMaxBlurPixels = 24.0;
    public const double DefaultStartProgress = 0.30;
    public const double DefaultFullProgress = 0.60;

    /// <summary>Maximum blur radius in SOURCE-frame pixels (the captured
    /// desktop's pixel size), reached once the intensity envelope saturates.
    /// The per-frame radius is mask × envelope(p) × maxBlur, so this is the
    /// single "how blurry can it get" knob (M2.5 will expose it in the
    /// parameter panel). Validated to (0, 64]: above ~64 source pixels the
    /// 13-tap kernel starts banding; zero means the pass does nothing.</summary>
    public double MaxBlurPixels { get; init; } = DefaultMaxBlurPixels;

    /// <summary>Progress at which the effect envelope starts rising
    /// (2026-09-19 real-user tuning: with a linear curve the blur was
    /// invisible until the fold had already collapsed the image - the
    /// user asked for visible 虚化 from ~0.35). Below this progress the
    /// blur AND dim passes contribute nothing.</summary>
    public double StartProgress { get; init; } = DefaultStartProgress;

    /// <summary>Progress at which the envelope saturates at 1. Linear ramp
    /// between Start and Full; defaults chosen so the mid-fold (0.4-0.6)
    /// carries a clearly visible blur while the screen is still up.</summary>
    public double FullProgress { get; init; } = DefaultFullProgress;

    /// <summary>Effect intensity envelope: 0 before StartProgress, linear
    /// ramp to 1 at FullProgress, then 1. Applied by the wiring BEFORE the
    /// progress uniform reaches the shader, so blur AND dim follow the same
    /// curve (one envelope, one story) and the shader needs no change.</summary>
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

    /// <summary>Validates and returns the blur radius normalized to the given
    /// source frame height (the unit the shader works in). Throws on anything
    /// that would make the pass ill-defined.</summary>
    public double MaxBlurNormalized(int sourceFrameHeightPixels)
    {
        Validate();
        if (sourceFrameHeightPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceFrameHeightPixels),
                sourceFrameHeightPixels, "Source frame height must be positive.");
        }
        return MaxBlurPixels / sourceFrameHeightPixels;
    }

    /// <summary>Validates the knobs. Throws on anything that would render
    /// garbage.</summary>
    public void Validate()
    {
        if (double.IsNaN(MaxBlurPixels) || double.IsInfinity(MaxBlurPixels)
            || MaxBlurPixels <= 0.0 || MaxBlurPixels > 64.0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBlurPixels), MaxBlurPixels,
                "MaxBlurPixels must be within (0, 64] source pixels.");
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
