using System;
using DuoFlow.Render;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M1.8 model-v2 frosted blur knobs (DD-046): the max radius is the single
/// "how deep can the frost go" knob (the reference ships 160 device pixels
/// with mip pre-filtering), and the intensity envelope must be visible from
/// ~0.35 progress (2026-09-19 user tuning).
/// </summary>
public class BlurOptionsTests
{
    [Fact]
    public void Defaults_MatchReference()
    {
        BlurOptions o = new();
        Assert.Equal(160.0, o.MaxBlurPixels, 12);
        Assert.Equal(0.30, o.StartProgress, 12);
        Assert.Equal(0.60, o.FullProgress, 12);
        o.Validate();
    }

    [Fact]
    public void IntensityAt_FollowsLinearEnvelope()
    {
        BlurOptions o = new(); // start 0.30, full 0.60
        Assert.Equal(0.0, o.IntensityAt(0.0), 12);
        Assert.Equal(0.0, o.IntensityAt(0.3), 12);
        Assert.Equal(0.5, o.IntensityAt(0.45), 12);
        Assert.Equal(1.0, o.IntensityAt(0.6), 12);
        Assert.Equal(1.0, o.IntensityAt(1.0), 12);
        Assert.Equal(1.0, o.IntensityAt(1.7), 12);
    }

    [Fact]
    public void IntensityAt_VisibleByPointThreeFive()
    {
        BlurOptions o = new();
        Assert.True(o.IntensityAt(0.35) >= 0.1, $"f(0.35)={o.IntensityAt(0.35)}");
        Assert.True(o.IntensityAt(0.4) >= 0.3, $"f(0.4)={o.IntensityAt(0.4)}");
    }

    [Fact]
    public void IntensityAt_MonotoneAcrossRange()
    {
        BlurOptions o = new();
        double last = -1;
        for (int i = 0; i <= 100; i++)
        {
            double v = o.IntensityAt(i / 100.0);
            Assert.True(v >= last, $"envelope decreased at p={i / 100.0}");
            last = v;
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(400.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_RejectsInvalidRadius(double radius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BlurOptions { MaxBlurPixels = radius }.Validate());
    }

    [Theory]
    [InlineData(0.5, 0.4)]  // full <= start
    [InlineData(0.95, 1.0)] // start beyond the 0.9 cap
    public void Validate_RejectsBadEnvelope(double start, double full)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BlurOptions { StartProgress = start, FullProgress = full }.Validate());
    }
}
