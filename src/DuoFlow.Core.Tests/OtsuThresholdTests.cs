using System;
using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M3.2: the threshold that separates "screen" from "everything else".
/// The important properties are adaptivity (it must follow the room's
/// brightness) and the separability report (it must be able to say "there was
/// nothing to separate" instead of inventing a threshold).
/// </summary>
public class OtsuThresholdTests
{
    private static GrayImage Image(params byte[] pixels) => new(pixels.Length, 1, pixels);

    [Fact]
    public void TwoLevelImage_ThresholdSplitsThem()
    {
        var pixels = new byte[100];
        for (int i = 0; i < 100; i++)
        {
            pixels[i] = i < 60 ? (byte)20 : (byte)200;
        }

        OtsuResult result = OtsuThreshold.Compute(new GrayImage(10, 10, pixels));

        Assert.True(result.Threshold > 20 && result.Threshold <= 200,
            $"threshold {result.Threshold} should sit between the two modes");
        Assert.True(result.IsBimodal);
        Assert.True(result.Separability > 0.9);
    }

    [Fact]
    public void ThresholdUsesGreaterOrEqualForForeground()
    {
        // The contract is "pixels >= Threshold are foreground". With modes at
        // 0 and 255 that means the split must land at 1..255, never at 0 -
        // otherwise the dark mode counts as foreground too.
        var pixels = new byte[] { 0, 0, 255, 255 };
        OtsuResult result = OtsuThreshold.Compute(Image(pixels));

        Assert.True(result.Threshold >= 1);
        int foreground = 0;
        foreach (byte b in pixels)
        {
            if (b >= result.Threshold)
            {
                foreground++;
            }
        }

        Assert.Equal(2, foreground);
    }

    [Fact]
    public void UniformImage_ReportsZeroSeparability()
    {
        var pixels = new byte[64];
        Array.Fill(pixels, (byte)128);

        OtsuResult result = OtsuThreshold.Compute(new GrayImage(8, 8, pixels));

        Assert.False(result.IsBimodal);
        Assert.Equal(0.0, result.Separability, 6);
    }

    [Fact]
    public void EmptyInput_DoesNotThrow()
    {
        OtsuResult result = OtsuThreshold.Compute(ReadOnlySpan<byte>.Empty);
        Assert.Equal(0, result.Threshold);
        Assert.Equal(0.0, result.Separability);
    }

    [Fact]
    public void SeparabilityIsAlwaysInUnitRange()
    {
        var rng = new Random(7);
        for (int trial = 0; trial < 20; trial++)
        {
            var pixels = new byte[500];
            rng.NextBytes(pixels);
            OtsuResult result = OtsuThreshold.Compute(Image(pixels));

            Assert.InRange(result.Separability, 0.0, 1.0);
            Assert.InRange(result.Threshold, 0, 255);
        }
    }

    [Fact]
    public void SeparabilityIsComparableAcrossResolutions()
    {
        // A raw between-class variance grows with pixel count, so it would not
        // be comparable between a 480p frame and a 720p frame. Normalizing is
        // what makes "separability > 0.35" a statement about the scene.
        var small = new byte[100];
        var large = new byte[400];
        for (int i = 0; i < small.Length; i++)
        {
            small[i] = i % 2 == 0 ? (byte)30 : (byte)180;
        }

        for (int i = 0; i < large.Length; i++)
        {
            large[i] = i % 2 == 0 ? (byte)30 : (byte)180;
        }

        double a = OtsuThreshold.Compute(new GrayImage(10, 10, small)).Separability;
        double b = OtsuThreshold.Compute(new GrayImage(20, 20, large)).Separability;

        Assert.Equal(a, b, 3);
    }

    [Fact]
    public void AdaptsToOverallDarkening()
    {
        // Same scene, both modes shifted down by 60: the threshold must move
        // with them. A fixed threshold is exactly what this replaces.
        var bright = new byte[200];
        var dim = new byte[200];
        for (int i = 0; i < 200; i++)
        {
            bright[i] = i < 100 ? (byte)60 : (byte)200;
            dim[i] = i < 100 ? (byte)10 : (byte)140;
        }

        int tBright = OtsuThreshold.Compute(new GrayImage(20, 10, bright)).Threshold;
        int tDim = OtsuThreshold.Compute(new GrayImage(20, 10, dim)).Threshold;

        Assert.True(tDim < tBright, $"dim threshold {tDim} should be below bright {tBright}");
    }

    [Fact]
    public void GradientScene_ThresholdLandsInsideTheRange()
    {
        var pixels = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            pixels[i] = (byte)i;
        }

        OtsuResult result = OtsuThreshold.Compute(Image(pixels));
        Assert.InRange(result.Threshold, 60, 200);
    }
}
