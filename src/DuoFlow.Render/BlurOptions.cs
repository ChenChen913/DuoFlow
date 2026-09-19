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

    /// <summary>Maximum blur radius in SOURCE-frame pixels (the captured
    /// desktop's pixel size), reached at progress 1 inside the hinge zone.
    /// The per-frame radius is mask × progress × maxBlur, so this is the
    /// single "how blurry can it get" knob (M2.5 will expose it in the
    /// parameter panel). Validated to (0, 64]: above ~64 source pixels the
    /// 13-tap kernel starts banding; zero means the pass does nothing.</summary>
    public double MaxBlurPixels { get; init; } = DefaultMaxBlurPixels;

    /// <summary>Validates and returns the blur radius normalized to the given
    /// source frame height (the unit the shader works in). Throws on anything
    /// that would make the pass ill-defined.</summary>
    public double MaxBlurNormalized(int sourceFrameHeightPixels)
    {
        if (double.IsNaN(MaxBlurPixels) || double.IsInfinity(MaxBlurPixels)
            || MaxBlurPixels <= 0.0 || MaxBlurPixels > 64.0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBlurPixels), MaxBlurPixels,
                "MaxBlurPixels must be within (0, 64] source pixels.");
        }
        if (sourceFrameHeightPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceFrameHeightPixels),
                sourceFrameHeightPixels, "Source frame height must be positive.");
        }
        return MaxBlurPixels / sourceFrameHeightPixels;
    }
}
