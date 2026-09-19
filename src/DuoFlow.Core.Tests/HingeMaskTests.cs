using System;
using DuoFlow.Render;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M1.5 hinge mask (DD-040). The mask must be exactly 1 at the hinge center,
/// exactly 0 at distance ≥ HingeWidth, monotonically falling in between,
/// symmetric around the center, bounded to [0,1] everywhere, and its shape
/// must respond to the falloff exponent (else the knob is dead).
/// Default profile: center=0, width=0.35, exponent=2.
/// </summary>
public class HingeMaskTests
{
    private const double Eps = 1e-9;

    [Fact]
    public void Evaluate_Defaults_PeakAtHingeEdge()
    {
        HingeMaskProfile m = HingeMask.Build();

        Assert.Equal(0.0, m.HingeCenter, 12);
        Assert.Equal(0.35, m.HingeWidth, 12);
        Assert.Equal(2.0, m.FalloffExponent, 12);

        Assert.Equal(1.0, m.Evaluate(0.0), 12);       // exactly on the fold axis
        Assert.True(m.Evaluate(0.02) > 0.97);         // near-plateau close to the hinge
    }

    [Fact]
    public void Evaluate_ZeroBeyondWidth()
    {
        HingeMaskProfile m = HingeMask.Build(); // center=0, width=0.35
        Assert.Equal(0.0, m.Evaluate(0.35), 12);
        Assert.Equal(0.0, m.Evaluate(0.6), 12);
        Assert.Equal(0.0, m.Evaluate(1.0), 12);
    }

    [Fact]
    public void Evaluate_MonotoneFalling_AwayFromCenter()
    {
        HingeMaskProfile m = HingeMask.Build();
        double last = double.MaxValue;
        for (int i = 0; i <= 100; i++)
        {
            double v = m.Evaluate(i / 100.0);
            Assert.InRange(v, 0.0, 1.0);
            Assert.True(v <= last + Eps, $"mask increased at y={i / 100.0}");
            last = v;
        }
    }

    [Fact]
    public void Evaluate_SmoothstepMidpoint_MatchesExponentPower()
    {
        // At t=0.5 smoothstep is exactly 0.5, so mask = (1-0.5)^exponent.
        HingeMaskProfile linear = HingeMask.Build(new MaskOptions { FalloffExponent = 1.0 });
        Assert.Equal(0.5, linear.Evaluate(0.35 / 2.0), 12);

        HingeMaskProfile squared = HingeMask.Build(); // exponent 2
        Assert.Equal(0.25, squared.Evaluate(0.35 / 2.0), 12);

        // The exponent knob must actually change the curve at the same spot.
        Assert.True(linear.Evaluate(0.35 / 2.0) > squared.Evaluate(0.35 / 2.0));
    }

    [Fact]
    public void Evaluate_ExponentOne_MatchesClosedFormSweep()
    {
        // Full closed-form check against the documented formula.
        HingeMaskProfile m = HingeMask.Build(new MaskOptions
        {
            HingeCenter = 0.0,
            HingeWidth = 0.4,
            FalloffExponent = 1.0,
        });
        for (int i = 0; i <= 50; i++)
        {
            double y = i / 50.0;
            double t = Math.Min(y / 0.4, 1.0);
            double expected = 1.0 - t * t * (3.0 - 2.0 * t);
            Assert.Equal(expected, m.Evaluate(y), 9);
        }
    }

    [Fact]
    public void Evaluate_SymmetricAroundCenter_WhenCenterInsideScreen()
    {
        HingeMaskProfile m = HingeMask.Build(new MaskOptions { HingeCenter = 0.3, HingeWidth = 0.25 });
        Assert.Equal(1.0, m.Evaluate(0.3), 12);
        Assert.Equal(m.Evaluate(0.3 - 0.1), m.Evaluate(0.3 + 0.1), 12);
        Assert.Equal(m.Evaluate(0.3 - 0.2), m.Evaluate(0.3 + 0.2), 12);
        Assert.Equal(0.0, m.Evaluate(0.3 + 0.25), 12);
        Assert.Equal(0.0, m.Evaluate(0.3 - 0.25), 12); // below the edge of the screen, still defined
    }

    [Fact]
    public void Evaluate_Continuity_NoJumpsAnywhere()
    {
        foreach (double exponent in new[] { 1.0, 2.0, 4.0, 8.0 })
        {
            HingeMaskProfile m = HingeMask.Build(new MaskOptions { FalloffExponent = exponent });
            double last = m.Evaluate(0.0);
            for (int i = 1; i <= 200; i++)
            {
                double v = m.Evaluate(i / 200.0);
                // Guard against accidental discontinuities (branch cuts etc.).
                // The LEGAL max step grows with the exponent (exp=8 reaches
                // ~0.114 per 0.005 step near its steepest toe), so the bound
                // must tolerate that; a real discontinuity is O(0.3+).
                Assert.True(Math.Abs(v - last) < 0.15, $"jump at y={i / 200.0} exp={exponent}");
                last = v;
            }
        }
    }

    [Fact]
    public void Evaluate_NonFiniteY_Throws()
    {
        HingeMaskProfile m = HingeMask.Build();
        Assert.Throws<ArgumentOutOfRangeException>(() => m.Evaluate(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => m.Evaluate(double.PositiveInfinity));
    }

    [Theory]
    [InlineData(1.5)]   // center beyond the screen
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    public void Build_BadCenter_Throws(double center)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HingeMask.Build(new MaskOptions { HingeCenter = center }));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Build_BadWidth_Throws(double width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HingeMask.Build(new MaskOptions { HingeWidth = width }));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(8.1)]
    [InlineData(double.NaN)]
    public void Build_BadExponent_Throws(double exponent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HingeMask.Build(new MaskOptions { FalloffExponent = exponent }));
    }

    [Fact]
    public void Build_NullOptions_ReturnsDefaults()
    {
        HingeMaskProfile m = HingeMask.Build();
        Assert.Equal(MaskOptions.DefaultHingeCenter, m.HingeCenter, 12);
        Assert.Equal(MaskOptions.DefaultHingeWidth, m.HingeWidth, 12);
        Assert.Equal(MaskOptions.DefaultFalloffExponent, m.FalloffExponent, 12);
    }
}
