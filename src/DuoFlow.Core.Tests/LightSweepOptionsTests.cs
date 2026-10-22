using System;
using DuoFlow.Render;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M2.2 light sweep: a narrow neutral highlight travelling across the fold.
///
/// Three properties matter and are asserted here:
///
///  1. "off" must be a BIT-EXACT no-op, same contract as the gradient pass -
///     M2.0's acceptance numbers were measured against the v2 render;
///  2. the geometry is analytic: <see cref="LightSweepOptions.CenterAt"/> is a
///     pure function of progress, so "the band sweeps from the hinge to the far
///     edge" is a testable statement rather than something to eyeball;
///  3. <see cref="LightSweepOptions.Width"/> really is the FWHM - the band
///     weight must be exactly 0.5 at centre +/- Width/2. That is what makes the
///     knob meaningful instead of an arbitrary sigma.
///
/// The peak luminance gain is bounded by Strength * (1 - rgb) &lt;= Strength, so
/// the "peak delta &lt;= strength limit" acceptance criterion is a consequence of
/// the shader formula, not a separate clamp.
/// </summary>
public class LightSweepOptionsTests
{
    // FWHM -> sigma -> the half-maximum half-width, i.e. Width/2.
    private const double HalfWidthOfPointOne = 0.05;

    [Fact]
    public void Defaults_AreOff_AndValid()
    {
        LightSweepOptions o = new();
        Assert.False(o.Enabled);
        Assert.Equal(0.0, o.EffectiveStrength, 12);          // off wins
        Assert.Equal(0.08, o.Strength, 12);
        Assert.Equal(0.10, o.Width, 12);
        Assert.Equal(0.0, o.Position, 12);
        Assert.Equal(SweepDirection.HingeToFar, o.Direction);
        o.Validate();
    }

    [Fact]
    public void Disabled_IsExactlyZero_EvenWithStrengthSet()
    {
        LightSweepOptions o = new() { Enabled = false, Strength = 0.2 };
        Assert.True(o.EffectiveStrength == 0.0, "must be exactly 0.0, not 1e-17");
    }

    [Fact]
    public void EffectiveStrength_ClampsAtLimit()
    {
        Assert.Equal(LightSweepOptions.MaxStrengthLimit,
            new LightSweepOptions { Enabled = true, Strength = 0.9 }.EffectiveStrength, 12);
        Assert.Equal(LightSweepOptions.MaxStrengthLimit,
            new LightSweepOptions { Enabled = true, Strength = 0.2 }.EffectiveStrength, 12);
        Assert.Equal(0.05, new LightSweepOptions { Enabled = true, Strength = 0.05 }.EffectiveStrength, 12);
    }

    // ---- geometry ----------------------------------------------------

    [Fact]
    public void CenterAt_SweepsHingeToFarEdge()
    {
        LightSweepOptions o = new();
        Assert.Equal(0.0, o.CenterAt(0.0), 12);
        Assert.Equal(0.5, o.CenterAt(0.5), 12);
        Assert.Equal(1.0, o.CenterAt(1.0), 12);
    }

    [Fact]
    public void CenterAt_IsMonotone_AndClampedOutsideProgress()
    {
        LightSweepOptions o = new();
        double prev = -1.0;
        for (int i = 0; i <= 20; i++)
        {
            double c = o.CenterAt(i / 20.0);
            Assert.True(c >= prev - 1e-12, $"centre went backwards at p={i / 20.0}");
            prev = c;
        }
        // Out-of-range progress must not escape the fold.
        Assert.Equal(0.0, o.CenterAt(-3.0), 12);
        Assert.Equal(1.0, o.CenterAt(7.0), 12);
    }

    [Fact]
    public void CenterAt_FarToHinge_RunsTheOtherWay()
    {
        LightSweepOptions o = new() { Direction = SweepDirection.FarToHinge };
        Assert.Equal(1.0, o.CenterAt(0.0), 12);
        Assert.Equal(0.5, o.CenterAt(0.5), 12);
        Assert.Equal(0.0, o.CenterAt(1.0), 12);
    }

    [Fact]
    public void CenterAt_PositionRunsAhead_AndSaturates()
    {
        LightSweepOptions o = new() { Position = 0.3 };
        Assert.Equal(0.3, o.CenterAt(0.0), 12);
        Assert.Equal(0.8, o.CenterAt(0.5), 12);
        Assert.Equal(1.0, o.CenterAt(0.9), 12);   // clamped, never past the far edge
    }

    [Fact]
    public void CenterAt_NaNProgress_IsTreatedAsOpen()
    {
        // A NaN leaking in from a slider must not produce a NaN centre and a
        // black band - it degrades to "fully open", which renders nothing.
        Assert.Equal(0.0, new LightSweepOptions().CenterAt(double.NaN), 12);
    }

    // ---- band shape --------------------------------------------------

    [Fact]
    public void Width_ReallyIsTheFwhm()
    {
        LightSweepOptions o = new() { Width = 0.10 };
        double centre = o.CenterAt(0.5);
        Assert.Equal(1.0, o.Evaluate(centre, 0.5), 12);                       // peak
        Assert.Equal(0.5, o.Evaluate(centre - HalfWidthOfPointOne, 0.5), 9);  // -FWHM/2
        Assert.Equal(0.5, o.Evaluate(centre + HalfWidthOfPointOne, 0.5), 9);  // +FWHM/2
    }

    [Fact]
    public void Evaluate_IsSymmetric_AndDecaysAwayFromCentre()
    {
        LightSweepOptions o = new() { Width = 0.10 };
        double centre = o.CenterAt(0.5);
        Assert.Equal(o.Evaluate(centre - 0.05, 0.5), o.Evaluate(centre + 0.05, 0.5), 12);
        Assert.True(o.Evaluate(centre - 0.30, 0.5) < 0.001,
            "a Gaussian with FWHM 0.10 must be effectively gone 0.30 away");
        // Always in [0, 1]: the shader uses it as a mix weight.
        for (int i = 0; i <= 40; i++)
        {
            double v = o.Evaluate(i / 40.0, 0.5);
            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void NarrowerWidth_MeansAHigherPeakAtAFixedOffset()
    {
        // A monotonicity check on the knob itself: turning Width down must
        // sharpen the band, never broaden it.
        double atOffset = 0.10;
        double wide = new LightSweepOptions { Width = 0.40 }.Evaluate(atOffset, 0.0);
        double narrow = new LightSweepOptions { Width = 0.05 }.Evaluate(atOffset, 0.0);
        Assert.True(narrow < wide,
            $"narrow ({narrow:0.0000}) should fall off faster than wide ({wide:0.0000})");
    }

    // ---- envelope coupling: p = 0 identity ---------------------------

    [Fact]
    public void AtProgressZero_TheDeliveredStrengthIsExactlyZero()
    {
        // The shader multiplies by PhysicsParams.y (the blur envelope), which
        // BlurOptions.IntensityAt reports as 0 at progress 0. That is what makes
        // p=0 an identity even with the sweep enabled and Strength at its cap.
        LightSweepOptions sweep = new() { Enabled = true, Strength = 0.2 };
        BlurOptions blur = new();
        double delivered = sweep.EffectiveStrength * blur.IntensityAt(0.0);
        Assert.True(delivered == 0.0, "p=0 must deliver exactly 0.0 light");
        Assert.True(sweep.EffectiveStrength > 0.0, "sanity: the sweep itself is on");
    }

    // ---- validation --------------------------------------------------

    [Fact]
    public void Validate_RejectsOutOfRangeStrength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Strength = -0.01 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Strength = 0.21 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Strength = double.NaN }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Strength = double.PositiveInfinity }.Validate());
    }

    [Fact]
    public void Validate_RejectsOutOfRangeWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Width = 0.0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Width = 0.01 }.Validate());   // below MinWidth
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Width = 0.51 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Width = double.NaN }.Validate());
        // The bounds themselves are legal.
        new LightSweepOptions { Width = LightSweepOptions.MinWidth }.Validate();
        new LightSweepOptions { Width = LightSweepOptions.MaxWidth }.Validate();
    }

    [Fact]
    public void Validate_RejectsOutOfRangePosition()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Position = -0.01 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Position = 1.01 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Position = double.NaN }.Validate());
    }

    [Fact]
    public void Validate_RejectsUndefinedDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LightSweepOptions { Direction = (SweepDirection)7 }.Validate());
    }

    [Fact]
    public void SigmaAndExponent_AreFinite_AtBothWidthExtremes()
    {
        foreach (double w in new[] { LightSweepOptions.MinWidth, 0.10, LightSweepOptions.MaxWidth })
        {
            LightSweepOptions o = new() { Width = w };
            Assert.True(double.IsFinite(o.Sigma));
            Assert.True(double.IsFinite(o.InvTwoSigmaSquared));
            Assert.True(o.InvTwoSigmaSquared > 0.0);
        }
    }
}
