using System;
using DuoFlow.Render;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M2.1 gradient pass (DD-046 §迁移): a hue ramp laid over the frost, acting
/// on the SHADOW side only. Two properties matter and are asserted here:
///
///  1. "off" must be a BIT-EXACT no-op, because M2.0's acceptance numbers
///     were measured against the v2 render - a pass that leaks 1e-3 of tint
///     would silently invalidate them;
///  2. strength is capped at 0.2 and the cap is enforced on the RENDER side
///     too (<see cref="GradientOptions.EffectiveStrength"/>), not only in
///     Validate - the panel is not the only possible caller.
///
/// The shader's own identity (p=0 produces no tint) follows from
/// <see cref="BlurOptions.IntensityAt"/> being 0 there; that is asserted in
/// BlurOptionsTests.IntensityAt_FollowsLinearEnvelope.
/// </summary>
public class GradientOptionsTests
{
    [Fact]
    public void Defaults_AreOff_AndValid()
    {
        GradientOptions o = new();
        Assert.False(o.Enabled);
        Assert.Equal(0.0, o.Strength, 12);
        Assert.Equal(0.0, o.EffectiveStrength, 12);   // off wins over Strength
        Assert.Equal(205.0, o.HueStartDegrees, 12);
        Assert.Equal(275.0, o.HueEndDegrees, 12);
        o.Validate();
    }

    [Fact]
    public void Disabled_IsExactlyZero_EvenWithStrengthSet()
    {
        // The shader branches on > 0, so exactly 0.0 is what makes "off"
        // provably identical to the pre-M2.1 render.
        GradientOptions o = new() { Enabled = false, Strength = 0.2 };
        Assert.Equal(0.0, o.EffectiveStrength, 12);
        Assert.True(o.EffectiveStrength == 0.0, "must be exactly 0.0, not 1e-17");
    }

    [Fact]
    public void EffectiveStrength_IsClampedToTheLimit()
    {
        // A caller that skipped Validate must still not get past the cap.
        GradientOptions o = new() { Enabled = true, Strength = 5.0 };
        Assert.Equal(GradientOptions.MaxStrengthLimit, o.EffectiveStrength, 12);
        GradientOptions negative = new() { Enabled = true, Strength = -1.0 };
        Assert.Equal(0.0, negative.EffectiveStrength, 12);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Validate_AcceptsStrengthWithinTheLimit(double strength)
    {
        new GradientOptions { Enabled = true, Strength = strength }.Validate();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.2000001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_RejectsStrengthOutsideTheLimit(double strength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GradientOptions { Strength = strength }.Validate());
    }

    [Theory]
    [InlineData(double.NaN, 0.0)]
    [InlineData(0.0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0.0)]
    public void Validate_RejectsNonFiniteHues(double start, double end)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GradientOptions { HueStartDegrees = start, HueEndDegrees = end }.Validate());
    }

    [Fact]
    public void HueToRgb_HitsThePrimariesAndWraps()
    {
        // Sanity on the HSL->RGB conversion: the six 60-degree anchors.
        (double R, double G, double B) red = GradientOptions.HueToRgb(0);
        Assert.True(red.R > red.G && red.R > red.B, $"0 deg should be red-dominant, got {red}");

        (double R, double G, double B) green = GradientOptions.HueToRgb(120);
        Assert.True(green.G > green.R && green.G > green.B, $"120 deg should be green-dominant, got {green}");

        (double R, double G, double B) blue = GradientOptions.HueToRgb(240);
        Assert.True(blue.B > blue.R && blue.B > blue.G, $"240 deg should be blue-dominant, got {blue}");

        // Wrapping: -120 == 240, 480 == 120.
        (double R, double G, double B) neg = GradientOptions.HueToRgb(-120);
        Assert.Equal(blue.R, neg.R, 12);
        Assert.Equal(blue.G, neg.G, 12);
        Assert.Equal(blue.B, neg.B, 12);

        (double R, double G, double B) over = GradientOptions.HueToRgb(480);
        Assert.Equal(green.R, over.R, 12);
        Assert.Equal(green.G, over.G, 12);
        Assert.Equal(green.B, over.B, 12);
    }

    [Fact]
    public void Tints_AreTheEndpointsOfTheRamp()
    {
        GradientOptions o = new() { HueStartDegrees = 210, HueEndDegrees = 300 };
        Assert.Equal(GradientOptions.HueToRgb(210).R, o.TintStart.R, 12);
        Assert.Equal(GradientOptions.HueToRgb(300).B, o.TintEnd.B, 12);
    }

    [Fact]
    public void Tints_StayInsideTheUnitCube()
    {
        for (int h = 0; h < 360; h += 5)
        {
            (double R, double G, double B) t = GradientOptions.HueToRgb(h);
            Assert.InRange(t.R, 0.0, 1.0);
            Assert.InRange(t.G, 0.0, 1.0);
            Assert.InRange(t.B, 0.0, 1.0);
        }
    }
}
