using System;
using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M3.2: the pixel container and the one downscale everything downstream
/// depends on. The downscale is load-bearing for the scale-invariance promise,
/// so its averaging behaviour is pinned here rather than assumed.
/// </summary>
public class GrayImageTests
{
    [Fact]
    public void FromBgra_UsesRec601Weights()
    {
        // Pure green is the brightest channel to the eye; pure blue the dimmest.
        GrayImage green = GrayImage.FromBgra(1, 1, new byte[] { 0, 255, 0, 255 });
        GrayImage blue = GrayImage.FromBgra(1, 1, new byte[] { 255, 0, 0, 255 });
        GrayImage red = GrayImage.FromBgra(1, 1, new byte[] { 0, 0, 255, 255 });
        GrayImage black = GrayImage.FromBgra(1, 1, new byte[] { 0, 0, 0, 255 });
        GrayImage white = GrayImage.FromBgra(1, 1, new byte[] { 255, 255, 255, 255 });

        // Fixed-point 77/150/29 over 256: green lands on 149.7, so the
        // integer path may report either side of the rounding boundary.
        Assert.InRange(green[0, 0], 149, 150);
        Assert.Equal(29, blue[0, 0]);
        Assert.Equal(77, red[0, 0]);
        Assert.Equal(0, black[0, 0]);
        Assert.Equal(255, white[0, 0]);
        Assert.True(green[0, 0] > red[0, 0]);
        Assert.True(red[0, 0] > blue[0, 0]);
    }

    [Fact]
    public void FromBgra_IgnoresAlpha()
    {
        // NV12 has no alpha; whatever the conversion put there must not change
        // luminance, or the probe's AlphaMean check and this number would
        // disagree about the same frame.
        GrayImage a = GrayImage.FromBgra(1, 1, new byte[] { 10, 20, 30, 0 });
        GrayImage b = GrayImage.FromBgra(1, 1, new byte[] { 10, 20, 30, 255 });
        Assert.Equal(a[0, 0], b[0, 0]);
    }

    [Fact]
    public void FromBgra_RejectsShortBuffer()
    {
        Assert.Throws<ArgumentException>(() => GrayImage.FromBgra(2, 2, new byte[15]));
    }

    [Fact]
    public void FromBgra_AcceptsLongerBuffer()
    {
        // The camera hands out a buffer that may be padded; only the head is ours.
        GrayImage image = GrayImage.FromBgra(1, 1, new byte[64]);
        Assert.Equal(1, image.PixelCount);
    }

    [Fact]
    public void Constructor_RejectsMismatchedLength()
    {
        Assert.Throws<ArgumentException>(() => new GrayImage(4, 4, new byte[15]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrayImage(0, 4, Array.Empty<byte>()));
    }

    [Fact]
    public void Indexer_IsRowMajor()
    {
        var pixels = new byte[6] { 1, 2, 3, 4, 5, 6 };
        var image = new GrayImage(3, 2, pixels);

        Assert.Equal(1, image[0, 0]);
        Assert.Equal(3, image[2, 0]);
        Assert.Equal(4, image[0, 1]);
        Assert.Equal(6, image[2, 1]);
    }

    [Fact]
    public void Downscaled_ReturnsSameInstanceWhenAlreadySmall()
    {
        var image = new GrayImage(100, 50, new byte[5000]);
        Assert.Same(image, image.Downscaled(160));
        Assert.Same(image, image.Downscaled(100));
    }

    [Fact]
    public void Downscaled_RespectsMaxSideAndKeepsAspect()
    {
        var image = new GrayImage(1280, 720, new byte[1280 * 720]);
        GrayImage small = image.Downscaled(160);

        Assert.Equal(160, Math.Max(small.Width, small.Height));
        Assert.Equal(90, small.Height);   // 16:9 preserved
    }

    [Fact]
    public void Downscaled_AveragesTheBlock()
    {
        // 2x2 {0,0,0,100} -> 1x1 must be 25, not 0 (nearest neighbour picks the
        // first pixel) and not 100. Nearest neighbour here is the difference
        // between an edge that moves smoothly with resolution and one that jumps.
        var one = new GrayImage(2, 2, new byte[] { 0, 0, 0, 100 }).Downscaled(1);
        Assert.Equal(1, one.Width);
        Assert.Equal(1, one.Height);
        Assert.Equal(25, one[0, 0]);

        // 4x2 -> 2x1: each output pixel averages its own 2x2 column pair.
        var pixels = new byte[8] { 0, 0, 0, 100, 10, 10, 10, 10 };
        GrayImage half = new GrayImage(4, 2, pixels).Downscaled(2);

        Assert.Equal(2, half.Width);
        Assert.Equal(1, half.Height);
        Assert.Equal(5, half[0, 0]);    // (0 + 0 + 10 + 10) / 4
        Assert.Equal(30, half[1, 0]);   // (0 + 100 + 10 + 10) / 4
    }

    [Fact]
    public void Downscaled_UniformImageStaysUniform()
    {
        var pixels = new byte[16 * 16];
        Array.Fill(pixels, (byte)77);
        GrayImage small = new GrayImage(16, 16, pixels).Downscaled(4);

        foreach (byte b in small.Pixels)
        {
            Assert.Equal(77, b);
        }
    }

    [Fact]
    public void Downscaled_OfAThinImageKeepsAtLeastOnePixel()
    {
        var image = new GrayImage(400, 1, new byte[400]);
        GrayImage small = image.Downscaled(160);
        Assert.Equal(1, small.Height);
        Assert.True(small.Width >= 1);
    }
}
