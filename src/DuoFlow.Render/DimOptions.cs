using System;

namespace DuoFlow.Render;

/// <summary>
/// M1.7 Dimming knobs (DD-042). PROJECT_SPEC §13:
/// brightness = 1 − hingeMask × progress × maxDarkness - the near-hinge band
/// darkens progressively, the far screen stays at full brightness (the mask
/// is zero above its width), and a fully open desktop is untouched
/// (progress 0).
/// </summary>
public sealed class DimOptions
{
    public const double DefaultMaxDarkness = 0.8;

    /// <summary>Maximum brightness reduction as a fraction of full brightness,
    /// reached at progress 1 inside the hinge zone (mask = 1). 0.8 = the
    /// hinge area drops to 20% brightness at full fold. Validated to [0, 1]:
    /// negative values brighten (nonsense), above 1 inverts to negative
    /// light.</summary>
    public double MaxDarkness { get; init; } = DefaultMaxDarkness;

    /// <summary>Validates the knob. Throws on anything that would render
    /// garbage.</summary>
    public void Validate()
    {
        if (double.IsNaN(MaxDarkness) || double.IsInfinity(MaxDarkness)
            || MaxDarkness < 0.0 || MaxDarkness > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDarkness), MaxDarkness,
                "MaxDarkness must be within [0, 1] (a fraction of full brightness).");
        }
    }
}
