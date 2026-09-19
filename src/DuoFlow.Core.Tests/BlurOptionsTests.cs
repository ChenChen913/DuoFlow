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
        Assert.Equal(BlurOptions.DefaultMaxBlurPixels, new BlurOptions().MaxBlurPixels, 12);
    }
}
