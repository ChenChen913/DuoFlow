using System;

namespace DuoFlow.Render;

/// <summary>
/// M2.1 gradient pass: a hue ramp laid over the frosted fold.
///
/// WHERE IT ACTS: on the SHADOW, not on the lit side. The shader applies it
/// after the DD-046 light falloff and scales it by <c>1 - light</c>, so the
/// fold reads as coloured glass where it catches less light while the bright
/// hinge stays neutral. A flat additive tint there would roughly double the
/// far edge's luminance and undo the shadow DD-046 spent M1 building.
///
/// BOUNDS: strength is capped at <see cref="MaxStrengthLimit"/>. The pass is
/// meant to be a tint, not a colour grade - past 0.2 the frost stops reading
/// as glass and starts reading as a filter. The cap is enforced in
/// <see cref="Validate"/> (constructor-side) AND in <see cref="EffectiveStrength"/>
/// (render-side), because the panel is not the only possible caller.
/// </summary>
public sealed class GradientOptions
{
    /// <summary>Hard ceiling for <see cref="Strength"/>.</summary>
    public const double MaxStrengthLimit = 0.2;

    // Cool glass by default: a slight blue at the hinge drifting to violet at
    // the far edge. Both hues are deliberately in the same family - a ramp
    // across 180 degrees of hue reads as a rainbow, not as glass.
    public const double DefaultHueStartDegrees = 205.0;
    public const double DefaultHueEndDegrees = 275.0;

    /// <summary>
    /// Strength a freshly enabled gradient gets (DD-051). It used to default to
    /// 0 while the sweep defaulted to 0.08, so ticking "启用渐变色相" produced
    /// no visible change at all - a first-run papercut that reads as "the
    /// gradient is broken". The shader still treats <c>Enabled = false</c> as
    /// an exact no-op (<see cref="EffectiveStrength"/> returns 0.0), so this
    /// only affects what happens the moment the box is ticked.
    /// </summary>
    public const double DefaultStrength = 0.08;

    /// <summary>Off by default: M2.1 must ship as a no-op so the M2.0
    /// acceptance numbers (which measure the v2 render) stay valid until
    /// M2.3 decides otherwise.</summary>
    public bool Enabled { get; init; }

    /// <summary>Hue at the hinge (source-side height 0), in degrees. Any
    /// finite value is accepted; it is wrapped into [0, 360).</summary>
    public double HueStartDegrees { get; init; } = DefaultHueStartDegrees;

    /// <summary>Hue at the far edge (source-side height 1), in degrees.</summary>
    public double HueEndDegrees { get; init; } = DefaultHueEndDegrees;

    /// <summary>Mix amount in [0, <see cref="MaxStrengthLimit"/>].</summary>
    public double Strength { get; init; } = DefaultStrength;

    /// <summary>The strength the shader should actually receive: 0 when
    /// disabled, otherwise clamped. Returning exactly 0.0 (not 1e-9) is what
    /// makes "disabled" a bit-exact no-op in the pixel shader.</summary>
    public double EffectiveStrength
        => Enabled ? Math.Clamp(Strength, 0.0, MaxStrengthLimit) : 0.0;

    /// <summary>RGB tint at the hinge, each channel in [0, 1].</summary>
    public (double R, double G, double B) TintStart => HueToRgb(HueStartDegrees);

    /// <summary>RGB tint at the far edge, each channel in [0, 1].</summary>
    public (double R, double G, double B) TintEnd => HueToRgb(HueEndDegrees);

    /// <summary>Validates the knobs. Throws on anything that would render
    /// garbage - same contract as <see cref="BlurOptions.Validate"/>.</summary>
    public void Validate()
    {
        if (double.IsNaN(Strength) || double.IsInfinity(Strength)
            || Strength < 0.0 || Strength > MaxStrengthLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(Strength), Strength,
                $"Strength must be within [0, {MaxStrengthLimit}] (a tint, not a colour grade).");
        }
        if (double.IsNaN(HueStartDegrees) || double.IsInfinity(HueStartDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(HueStartDegrees), HueStartDegrees,
                "HueStartDegrees must be finite (it is wrapped into [0,360)).");
        }
        if (double.IsNaN(HueEndDegrees) || double.IsInfinity(HueEndDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(HueEndDegrees), HueEndDegrees,
                "HueEndDegrees must be finite (it is wrapped into [0,360)).");
        }
    }

    /// <summary>
    /// HSL -> RGB for a fully saturated-ish glass tint. Saturation and
    /// lightness are fixed rather than exposed: the pass only needs a HUE
    /// ramp, and letting callers set lightness would let them reintroduce the
    /// overexposure the strength cap exists to prevent.
    /// </summary>
    public static (double R, double G, double B) HueToRgb(double hueDegrees)
    {
        const double S = 0.65;
        const double L = 0.55;

        double h = hueDegrees % 360.0;
        if (h < 0.0)
        {
            h += 360.0;
        }
        double c = (1.0 - Math.Abs(2.0 * L - 1.0)) * S;
        double hp = h / 60.0;
        double x = c * (1.0 - Math.Abs(hp % 2.0 - 1.0));
        double m = L - c / 2.0;

        double r, g, b;
        if (hp < 1.0) { r = c; g = x; b = 0.0; }
        else if (hp < 2.0) { r = x; g = c; b = 0.0; }
        else if (hp < 3.0) { r = 0.0; g = c; b = x; }
        else if (hp < 4.0) { r = 0.0; g = x; b = c; }
        else if (hp < 5.0) { r = x; g = 0.0; b = c; }
        else { r = c; g = 0.0; b = x; }

        return (Math.Clamp(r + m, 0.0, 1.0),
                Math.Clamp(g + m, 0.0, 1.0),
                Math.Clamp(b + m, 0.0, 1.0));
    }
}
