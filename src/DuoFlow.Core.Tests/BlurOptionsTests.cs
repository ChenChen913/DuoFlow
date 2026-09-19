using System;
using DuoFlow.Render;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M1.6 blur knobs (DD-041): the max radius must be positive and capped (the
/// 13-tap kernel bands above ~64 source pixels), and the normalized value is
/// relative to the source frame height.
/// </summary>
public class BlurOptionsTests
{
    [Fact]
    public void MaxBlurNormalized_ConvertsAgainstSourceHeight()
    {
        BlurOptions o = new();
        Assert.Equal(24.0 / 1080, o.MaxBlurNormalized(1080), 12);
        Assert.Equal(24.0 / 720, o.MaxBlurNormalized(720), 12); // same pixels, smaller frame = stronger
    }

    [Fact]
    public void MaxBlurNormalized_AcceptsTinyValue()
    {
        BlurOptions o = new() { MaxBlurPixels = 0.5 };
        Assert.Equal(0.5 / 1080, o.MaxBlurNormalized(1080), 12);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(64.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void MaxBlurNormalized_RejectsInvalidRadius(double radius)
    {
        BlurOptions o = new() { MaxBlurPixels = radius };
        Assert.Throws<ArgumentOutOfRangeException>(() => o.MaxBlurNormalized(1080));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void MaxBlurNormalized_RejectsBadFrameHeight(int height)
    {
        BlurOptions o = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => o.MaxBlurNormalized(height));
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        BlurOptions o = new();
        Assert.Equal(BlurOptions.DefaultMaxBlurPixels, o.MaxBlurPixels, 12);
        o.Validate(); // must not throw
    }

    // -------------------------------------------------------------
    // M1.6 tuning (2026-09-19 user feedback): the effect envelope must make
    // the blur visible from ~0.35 instead of only after the fold collapsed.
    // -------------------------------------------------------------

    [Fact]
    public void IntensityAt_FollowsLinearEnvelope()
    {
        BlurOptions o = new(); // start 0.30, full 0.60
        Assert.Equal(0.0, o.IntensityAt(0.0), 12);      // nothing before start
        Assert.Equal(0.0, o.IntensityAt(0.3), 12);      // exactly at start
        Assert.Equal(0.5, o.IntensityAt(0.45), 12);     // mid-ramp
        Assert.Equal(1.0, o.IntensityAt(0.6), 12);      // saturated
        Assert.Equal(1.0, o.IntensityAt(1.0), 12);      // stays saturated
        Assert.Equal(1.0, o.IntensityAt(1.7), 12);      // clamped above 1
    }

    [Fact]
    public void IntensityAt_VisibleByPointThreeFive()
    {
        // The user-visible requirement: at 0.35 the envelope already carries
        // enough intensity for the blur to be perceptible (>= 0.1 of max).
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
    [InlineData(0.5, 0.4)]  // full <= start
    [InlineData(0.95, 1.0)] // start beyond the 0.9 cap
    public void Validate_RejectsBadEnvelope(double start, double full)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BlurOptions { StartProgress = start, FullProgress = full }.Validate());
    }
}
