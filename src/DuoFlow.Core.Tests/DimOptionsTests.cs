using System;
using DuoFlow.Render;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M1.7 dimming knobs (DD-042): MaxDarkness is a fraction of full brightness,
/// reached at progress 1 where the mask is 1. 0 = no dimming at all,
/// 1 = black hinge at full fold.
/// </summary>
public class DimOptionsTests
{
    [Fact]
    public void Defaults_MatchProjectSpec()
    {
        DimOptions o = new();
        Assert.Equal(DimOptions.DefaultMaxDarkness, o.MaxDarkness, 12);
        o.Validate(); // must not throw
    }

    [Fact]
    public void Validate_AcceptsBoundaries()
    {
        new DimOptions { MaxDarkness = 0.0 }.Validate(); // dimming disabled
        new DimOptions { MaxDarkness = 1.0 }.Validate(); // black hinge at full fold
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_RejectsInvalidFractions(double v)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DimOptions { MaxDarkness = v }.Validate());
    }
}
