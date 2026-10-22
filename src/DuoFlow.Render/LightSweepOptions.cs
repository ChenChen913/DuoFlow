using System;

namespace DuoFlow.Render;

/// <summary>Which way the light sweep travels across the fold.</summary>
public enum SweepDirection
{
    /// <summary>From the hinge towards the far edge as the fold closes (default).</summary>
    HingeToFar = 0,

    /// <summary>From the far edge back towards the hinge.</summary>
    FarToHinge = 1,
}

/// <summary>
/// M2.2 light sweep: a narrow neutral highlight running parallel to the hinge,
/// travelling across the fold as progress goes 0 -> 1.
///
/// WHAT IT IS, AND WHAT IT IS NOT: this is <b>light</b>, not colour. It is added
/// equally to all three channels, so it brightens without shifting hue - the
/// hue ramp belongs to <see cref="GradientOptions"/> and the two passes are
/// deliberately orthogonal (see DD-050).
///
/// BOUNDS: <see cref="Strength"/> is capped at <see cref="MaxStrengthLimit"/>,
/// so the peak luminance gain is at most 0.2 (51/255). The cap is also what the
/// shader uses to normalise its headroom-to-white guard: the gain is multiplied
/// by <c>saturate((1 - maxChannel) / MaxStrengthLimit)</c>, and because
/// <c>gain &lt;= MaxStrengthLimit</c> the sum can never exceed 1.0. That makes
/// "no clipping" and "peak delta &lt;= strength cap" true by construction rather
/// than by a separate clamp - which is why the cap is exported to the shader
/// (SweepParams.w) instead of being duplicated as a literal in HLSL.
///
/// <see cref="Width"/> is the FULL WIDTH AT HALF MAXIMUM in fold-height units,
/// which is the number a person tuning this actually thinks in ("how wide is
/// the band").
/// </summary>
public sealed class LightSweepOptions
{
    /// <summary>Hard ceiling for <see cref="Strength"/>.</summary>
    public const double MaxStrengthLimit = 0.2;

    /// <summary>Narrowest usable band (FWHM, fold-height units). Below this the
    /// band is thinner than the alpha feather and reads as a hard line.</summary>
    public const double MinWidth = 0.02;

    /// <summary>Widest usable band. Past 0.5 the "band" is most of the fold and
    /// stops reading as a sweep.</summary>
    public const double MaxWidth = 0.5;

    public const double DefaultWidth = 0.10;
    public const double DefaultStrength = 0.08;   // spec suggests 0.05 - 0.15
    public const double DefaultPosition = 0.0;

    /// <summary>FWHM -> sigma for a Gaussian: FWHM = 2*sqrt(2*ln2)*sigma.</summary>
    private const double FwhmToSigma = 2.3548200450309493;

    /// <summary>Off by default: like M2.1, the pass must ship as a no-op so the
    /// existing acceptance numbers stay valid until M2.3 decides otherwise.</summary>
    public bool Enabled { get; init; }

    /// <summary>Phase advance along the sweep direction, in fold-height units
    /// within [0, 1]. 0 = the band sits exactly where progress puts it.
    /// Positive values run the band AHEAD of the fold, which is the only
    /// direction that reads well: a band lagging behind would park in the
    /// already-sharp hinge zone and look like a lens flare.</summary>
    public double Position { get; init; } = DefaultPosition;

    /// <summary>Full width at half maximum, in fold-height units.</summary>
    public double Width { get; init; } = DefaultWidth;

    /// <summary>Peak mix amount in [0, <see cref="MaxStrengthLimit"/>].</summary>
    public double Strength { get; init; } = DefaultStrength;

    /// <summary>Which way the band travels.</summary>
    public SweepDirection Direction { get; init; } = SweepDirection.HingeToFar;

    /// <summary>The strength the shader should actually receive: exactly 0.0
    /// when disabled (not 1e-9), which is what makes "off" a bit-exact no-op -
    /// the shader branches on <c>&gt; 0.0</c> rather than adding a zero.</summary>
    public double EffectiveStrength
        => Enabled ? Math.Clamp(Strength, 0.0, MaxStrengthLimit) : 0.0;

    /// <summary>Gaussian sigma implied by <see cref="Width"/>.</summary>
    public double Sigma => Math.Max(Width, MinWidth) / FwhmToSigma;

    /// <summary>Pre-computed exponent factor the shader needs: 1 / (2*sigma^2).</summary>
    public double InvTwoSigmaSquared
    {
        get
        {
            double sigma = Sigma;
            return 1.0 / (2.0 * sigma * sigma);
        }
    }

    /// <summary>
    /// Band centre in fold-height units at the given progress: 0 is the hinge,
    /// 1 is the far edge. Pure function of progress, so it is unit-testable
    /// without a GPU.
    /// </summary>
    public double CenterAt(double progress)
    {
        double p = double.IsNaN(progress) ? 0.0 : Math.Clamp(progress, 0.0, 1.0);
        double travel = Math.Clamp(p + Math.Clamp(Position, 0.0, 1.0), 0.0, 1.0);
        return Direction == SweepDirection.FarToHinge ? 1.0 - travel : travel;
    }

    /// <summary>
    /// Band weight in [0, 1] at a source-side fold height. Deliberately
    /// independent of <see cref="Enabled"/> and of the blur envelope: this is
    /// the pure Gaussian, so tests can assert the FWHM definition directly.
    /// </summary>
    public double Evaluate(double sy, double progress)
    {
        double d = sy - CenterAt(progress);
        return Math.Exp(-(d * d) * InvTwoSigmaSquared);
    }

    /// <summary>Validates the knobs. Throws on anything that would render
    /// garbage - same contract as <see cref="BlurOptions.Validate"/>.</summary>
    public void Validate()
    {
        if (double.IsNaN(Strength) || double.IsInfinity(Strength)
            || Strength < 0.0 || Strength > MaxStrengthLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(Strength), Strength,
                $"Strength must be within [0, {MaxStrengthLimit}] (a glint, not a lamp).");
        }
        if (double.IsNaN(Width) || double.IsInfinity(Width)
            || Width < MinWidth || Width > MaxWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), Width,
                $"Width (FWHM) must be within [{MinWidth}, {MaxWidth}].");
        }
        if (double.IsNaN(Position) || double.IsInfinity(Position)
            || Position < 0.0 || Position > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(Position), Position,
                "Position must be within [0, 1].");
        }
        if (Direction != SweepDirection.HingeToFar && Direction != SweepDirection.FarToHinge)
        {
            throw new ArgumentOutOfRangeException(nameof(Direction), Direction,
                "Direction must be a defined SweepDirection.");
        }
    }
}
