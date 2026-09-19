using System;

namespace DuoFlow.Render;

/// <summary>
/// M1.5 Hinge Mask knobs (DD-040). PROJECT_SPEC §11 requires the mask to be
/// configurable (Hinge Position / Hinge Width / Falloff / Direction); the
/// direction comes from the WARP's hinge frame (DD-039: mask and warp MUST
/// share one hinge definition, otherwise the mask peaks off the fold line),
/// so this options type deliberately has no direction flag - the mask is
/// evaluated in whatever hinge frame the shader hands it.
/// </summary>
public sealed class MaskOptions
{
    public const double DefaultHingeCenter = 0.0;
    public const double DefaultHingeWidth = 0.35;
    public const double DefaultFalloffExponent = 2.0;

    /// <summary>Normalized distance of the hinge region's center from the
    /// hinge edge, in hinge-frame units (0 = exactly on the fold axis, which
    /// is the default and matches the warp; larger values move the peak
    /// into the screen, e.g. for a laptop whose hinge sits below the panel).
    /// PROJECT_SPEC §11 "Hinge Position".</summary>
    public double HingeCenter { get; init; } = DefaultHingeCenter;

    /// <summary>Normalized width of the falloff band: the mask reaches 0 at
    /// distance HingeWidth from the hinge center. PROJECT_SPEC §11
    /// "Hinge Width".</summary>
    public double HingeWidth { get; init; } = DefaultHingeWidth;

    /// <summary>Shape of the falloff: the base curve is
    /// (1 - smoothstep(t)) raised to this exponent. 1 = inverse smoothstep;
    /// larger values concentrate the mask near the hinge with a longer flat
    /// tail. Restricted to [1, 8]: exponents below 1 give the curve an
    /// unbounded slope at its zero crossing (visually a hard toe, breaking
    /// PROJECT_SPEC's "falloff must be smooth"). PROJECT_SPEC §11 "Falloff".</summary>
    public double FalloffExponent { get; init; } = DefaultFalloffExponent;
}

/// <summary>
/// One evaluation profile of the M1.5 hinge mask: mask(y) ∈ [0,1] over the
/// hinge frame (y = 0 at the hinge edge, 1 at the far edge), peaked at
/// <see cref="HingeCenter"/> and falling to 0 at HingeWidth away from it.
///
/// The downstream passes (M1.6 blur, M1.7 dimming) scale this by progress:
/// blurAmount = mask × progress × maxBlur (TECHNICAL_PROPOSAL §9/§10) - the
/// mask itself is progress-independent and static per configuration, which
/// is why it is built once and evaluated per pixel in the shader.
/// Pure data + one pure function: no D3D types, fully unit testable.
/// </summary>
public sealed record HingeMaskProfile(
    double HingeCenter,
    double HingeWidth,
    double FalloffExponent)
{
    /// <summary>
    /// Mask value at hinge-frame height y. Symmetric around HingeCenter,
    /// exactly 1 at the center, exactly 0 at distance ≥ HingeWidth.
    /// </summary>
    public double Evaluate(double y)
    {
        if (double.IsNaN(y) || double.IsInfinity(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), y,
                "Hinge-frame coordinate must be finite.");
        }

        double d = Math.Abs(y - HingeCenter);
        double t = Math.Clamp(d / HingeWidth, 0.0, 1.0);
        double s = t * t * (3.0 - 2.0 * t);            // smoothstep(0,1,t)
        double m = Math.Pow(1.0 - s, FalloffExponent); // FalloffExponent > 0 (validated)
        return Math.Clamp(m, 0.0, 1.0);
    }
}

/// <summary>Builds the hinge mask profile from options.</summary>
public static class HingeMask
{
    /// <summary>
    /// Validates options and builds the profile. Throws on anything that
    /// would make the mask ill-defined (the render side has no meaningful
    /// fallback and must not render garbage silently).
    /// </summary>
    public static HingeMaskProfile Build(MaskOptions? options = null)
    {
        options ??= new MaskOptions();

        if (double.IsNaN(options.HingeCenter) || double.IsInfinity(options.HingeCenter)
            || options.HingeCenter < 0.0 || options.HingeCenter > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.HingeCenter,
                "HingeCenter must be within [0, 1] (normalized distance from the hinge edge).");
        }
        if (double.IsNaN(options.HingeWidth) || double.IsInfinity(options.HingeWidth)
            || options.HingeWidth <= 0.0 || options.HingeWidth > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.HingeWidth,
                "HingeWidth must be within (0, 1].");
        }
        if (double.IsNaN(options.FalloffExponent) || double.IsInfinity(options.FalloffExponent)
            || options.FalloffExponent < 1.0 || options.FalloffExponent > 8.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.FalloffExponent,
                "FalloffExponent must be within [1, 8] (below 1 the falloff toe is not smooth).");
        }

        return new HingeMaskProfile(
            HingeCenter: options.HingeCenter,
            HingeWidth: options.HingeWidth,
            FalloffExponent: options.FalloffExponent);
    }
}
