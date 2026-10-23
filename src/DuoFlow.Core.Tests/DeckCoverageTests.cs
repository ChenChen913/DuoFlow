using System;
using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// Synthetic camera frames for M3.3, built the way the real scene looks: a far
/// surface on top, a near surface climbing up from the bottom edge.
///
/// <para>
/// Everything is expressed as a FRACTION of the frame, so the same spec renders
/// at any resolution - the precondition for the scale-invariance test. If the
/// spec carried pixels, rendering it at two sizes would produce two different
/// scenes and the test would prove nothing.
/// </para>
/// </summary>
internal static class DeckScene
{
    /// <summary>
    /// Near surface occupying the bottom <paramref name="coverage"/> of the
    /// frame. Defaults are a dark deck (60) under a bright far scene (200),
    /// which is what a laptop on a desk actually looks like from the lid
    /// camera: the deck is the dark thing and the room is the bright thing.
    /// </summary>
    public static GrayImage Render(
        int width,
        int height,
        double coverage,
        byte far = 200,
        byte near = 60,
        int noise = 0,
        int seed = 7)
    {
        var pixels = new byte[width * height];
        int split = (int)Math.Round(height * (1.0 - coverage));
        var rng = new Lcg(seed);

        for (int y = 0; y < height; y++)
        {
            byte value = y >= split ? near : far;
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int v = value + (noise > 0 ? rng.Next(noise) : 0);
                pixels[row + x] = (byte)Math.Clamp(v, 0, 255);
            }
        }

        return new GrayImage(width, height, pixels);
    }

    public static GrayImage Uniform(int width, int height, byte value, int noise = 0, int seed = 7)
    {
        var pixels = new byte[width * height];
        var rng = new Lcg(seed);
        for (int i = 0; i < pixels.Length; i++)
        {
            int v = value + (noise > 0 ? rng.Next(noise) : 0);
            pixels[i] = (byte)Math.Clamp(v, 0, 255);
        }

        return new GrayImage(width, height, pixels);
    }

    /// <summary>Overwrites one horizontal band, in fractions of the frame height.</summary>
    public static GrayImage WithBand(GrayImage image, double fromY, double toY, byte value)
    {
        var pixels = new byte[image.PixelCount];
        image.Pixels.CopyTo(pixels);

        int y0 = (int)Math.Round(image.Height * fromY);
        int y1 = (int)Math.Round(image.Height * toY);
        for (int y = y0; y < y1 && y < image.Height; y++)
        {
            int row = y * image.Width;
            for (int x = 0; x < image.Width; x++)
            {
                pixels[row + x] = value;
            }
        }

        return new GrayImage(image.Width, image.Height, pixels);
    }

    /// <summary>Overwrites one vertical band, in fractions of the frame width.</summary>
    public static GrayImage WithColumn(GrayImage image, double fromX, double toX, byte value)
    {
        var pixels = new byte[image.PixelCount];
        image.Pixels.CopyTo(pixels);

        int x0 = (int)Math.Round(image.Width * fromX);
        int x1 = (int)Math.Round(image.Width * toX);
        for (int y = 0; y < image.Height; y++)
        {
            int row = y * image.Width;
            for (int x = x0; x < x1 && x < image.Width; x++)
            {
                pixels[row + x] = value;
            }
        }

        return new GrayImage(image.Width, image.Height, pixels);
    }

    private sealed class Lcg
    {
        private uint _state;

        public Lcg(int seed) => _state = seed == 0 ? 1u : (uint)seed;

        public int Next(int amplitude)
        {
            _state = (_state * 1664525u) + 1013904223u;
            int span = (2 * amplitude) + 1;
            return (int)((_state >> 8) % (uint)span) - amplitude;
        }
    }
}

public class DeckCoverageTests
{
    /// <summary>
    /// The whole point of the estimator: a bigger bottom band must read as a
    /// bigger coverage. 160x100 divides exactly, so the only error is the
    /// pixel rounding of the band edge.
    /// </summary>
    [Theory]
    [InlineData(0.10)]
    [InlineData(0.25)]
    [InlineData(0.40)]
    [InlineData(0.55)]
    [InlineData(0.70)]
    [InlineData(0.85)]
    public void CoverageTracksTheBottomBand(double expected)
    {
        GrayImage frame = DeckScene.Render(160, 100, expected);

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.Equal(DeckCoverageReason.Ok, result.Reason);
        Assert.InRange(result.Coverage, expected - 0.02, expected + 0.02);
        Assert.True(result.IsUsable);
    }

    /// <summary>
    /// Polarity must be read, not assumed. A pale deck under a dark shelf
    /// inverts the picture and the estimator has to follow it - the same
    /// lesson M3.2 learned, kept because the deck really can be either.
    /// </summary>
    [Fact]
    public void NearSurfaceIsFoundWhenItIsTheBrightOne()
    {
        GrayImage frame = DeckScene.Render(160, 100, 0.50, far: 60, near: 200);

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.Equal(DeckCoverageReason.Ok, result.Reason);
        Assert.InRange(result.Coverage, 0.48, 0.52);
    }

    /// <summary>
    /// Only the region that REACHES THE BOTTOM EDGE counts.
    ///
    /// <para>
    /// Three bands: a bright strip on top, a dark band, a bright band at the
    /// bottom. The two bright bands are the same brightness and the same size,
    /// so an estimator that just took "the largest region of the near class"
    /// would be a coin flip - and an estimator that summed them would report
    /// 0.60. The right answer is 0.30: the near surface is defined by touching
    /// the bottom of the frame, because that is where a downward-looking camera
    /// pushes it.
    /// </para>
    /// </summary>
    [Fact]
    public void OnlyTheBottomConnectedRegionCounts()
    {
        // Bottom 30% bright, middle 40% dark, top 30% bright again.
        GrayImage frame = DeckScene.Render(160, 100, 0.30, far: 60, near: 200);
        frame = DeckScene.WithBand(frame, 0.0, 0.30, 200);   // bright strip up top

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.Equal(DeckCoverageReason.Ok, result.Reason);
        Assert.InRange(result.Coverage, 0.28, 0.32);
        Assert.InRange(result.NearEdgeTop, 0.68, 0.72);
    }

    /// <summary>
    /// Same scene, four times the pixels, same answer. This is the property
    /// that makes "no pixel coordinates" enforceable rather than aspirational:
    /// a detector that secretly depends on the pixel count fails here.
    /// </summary>
    [Theory]
    [InlineData(160, 100)]
    [InlineData(320, 200)]
    [InlineData(640, 400)]
    public void CoverageIsScaleInvariant(int width, int height)
    {
        GrayImage frame = DeckScene.Render(width, height, 0.45);

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.Equal(DeckCoverageReason.Ok, result.Reason);
        Assert.InRange(result.Coverage, 0.43, 0.47);
    }

    /// <summary>
    /// A uniform frame is 100% surface - but the estimator must NOT claim to
    /// know which surface. Coverage 1.0 is the honest reading of the pixels;
    /// whether that means "the lid is shut" or "the lens is capped" is decided
    /// by LidProgressTracker, which has the history to tell them apart.
    /// </summary>
    [Fact]
    public void FlatFrameIsReportedAsFullCoverageWithoutJudging()
    {
        GrayImage frame = DeckScene.Uniform(160, 100, 128);

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.Equal(DeckCoverageReason.FlatFrame, result.Reason);
        Assert.Equal(1.0, result.Coverage, 6);
        Assert.Equal(1.0, result.Confidence, 6);
        Assert.True(result.FillsFrame);
        Assert.False(result.IsUsable);   // only Reason.Ok is consumable
    }

    /// <summary>
    /// Sensor noise on a uniform surface is still a flat frame. Without the
    /// absolute standard-deviation guard, Otsu would split the noise floor into
    /// "two modes" and report a confident region - the exact failure M3.2 hit
    /// on a capped lens at up to 0.57 confidence.
    /// </summary>
    [Fact]
    public void NoisyFlatFrameIsStillFlat()
    {
        GrayImage frame = DeckScene.Uniform(160, 100, 128, noise: 1);

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.Equal(DeckCoverageReason.FlatFrame, result.Reason);
    }

    /// <summary>
    /// A near-class speck that does not reach the bottom edge is not a surface.
    /// Here only the last row is dark, so the near class exists but is 0.6% of
    /// the frame - below MinAreaFraction, and it is correctly rejected rather
    /// than turned into a confident coverage of 0.006.
    /// </summary>
    [Fact]
    public void SpeckOnTheBottomEdgeIsNotASurface()
    {
        GrayImage frame = DeckScene.Render(160, 100, 0.0, far: 200, near: 128);
        frame = DeckScene.WithBand(frame, 0.99, 1.0, 128);

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.Equal(DeckCoverageReason.NoNearSurface, result.Reason);
        Assert.False(result.IsUsable);
    }

    /// <summary>
    /// Two nearly identical brightnesses (100 vs 105): the region is real, but
    /// the evidence for its boundary is not. It must be reported as
    /// LowConfidence instead of silently treated as a good measurement -
    /// otherwise M3.3 would follow a boundary that is 5 gray levels of noise.
    /// </summary>
    [Fact]
    public void LowContrastSceneIsLowConfidence()
    {
        GrayImage frame = DeckScene.Render(160, 100, 0.50, far: 100, near: 105);

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.Equal(DeckCoverageReason.LowConfidence, result.Reason);
        Assert.False(result.IsUsable);
        Assert.InRange(result.ClassContrast, 4.0, 6.0);
    }

    /// <summary>
    /// No frame may ever throw. Every rejection is a value with a reason, so
    /// the provider can degrade instead of the camera thread dying silently.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(7, 7)]
    [InlineData(8, 8)]
    [InlineData(200, 3)]
    [InlineData(3, 200)]
    public void DegenerateFramesNeverThrow(int width, int height)
    {
        var pixels = new byte[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i % 256);
        }

        GrayImage frame = new(width, height, pixels);

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.True(Enum.IsDefined(result.Reason));
        Assert.InRange(result.Coverage, 0.0, 1.0);
        Assert.InRange(result.Confidence, 0.0, 1.0);
    }

    /// <summary>A frame whose whole area is the near surface is flagged as filling the frame.</summary>
    [Fact]
    public void NearlyFullFrameIsFlaggedAsFilling()
    {
        GrayImage frame = DeckScene.Render(160, 100, 0.97);

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.Equal(DeckCoverageReason.Ok, result.Reason);
        Assert.True(result.FillsFrame);
    }

    /// <summary>
    /// A region that spans the FULL FRAME HEIGHT is rejected.
    ///
    /// <para>
    /// This is the scene the development laptop actually produced with the lid
    /// fully open: a dark area covering ~half the frame, reaching both the top
    /// and bottom edges. It is not the surface below the camera, it is a
    /// left/right brightness split, and its area drifts with the room. Measured
    /// on the real machine: 0.491 -> 0.551 over 30 stationary frames, i.e. a
    /// 6-point drift that the previous build turned into a fake 0 -> 0.13
    /// progress ramp at confidence 1.0.
    /// </para>
    /// <para>
    /// Coverage is DEFINED as the area below the near surface's far edge. With
    /// no far edge in the frame there is nothing to measure, so the honest
    /// answer is "no measurement" rather than a confident number.
    /// </para>
    /// </summary>
    [Fact]
    public void RegionSpanningTheFullHeightIsRejected()
    {
        GrayImage frame = DeckScene.Uniform(160, 100, 200);
        frame = DeckScene.WithColumn(frame, 0.0, 0.60, 60);

        DeckCoverage result = DeckCoverageEstimator.Estimate(frame);

        Assert.Equal(DeckCoverageReason.NoFarBoundary, result.Reason);
        Assert.True(result.TouchesTop);
        Assert.False(result.IsUsable);
    }

    /// <summary>A genuine bottom band has a far boundary above it and must not be rejected.</summary>
    [Fact]
    public void BottomBandIsNotConfusedWithAFullHeightRegion()
    {
        DeckCoverage result = DeckCoverageEstimator.Estimate(DeckScene.Render(160, 100, 0.5));

        Assert.Equal(DeckCoverageReason.Ok, result.Reason);
        Assert.False(result.TouchesTop);
        Assert.InRange(result.NearEdgeTop, 0.48, 0.52);
    }

    /// <summary>The trace line must be printable and must not contain pixels.</summary>
    [Fact]
    public void DescribeIsSafeAndInformative()
    {
        DeckCoverage result = DeckCoverageEstimator.Estimate(DeckScene.Render(160, 100, 0.4));

        string text = result.Describe();

        Assert.Contains("coverage=", text, StringComparison.Ordinal);
        Assert.Contains("Ok", text, StringComparison.Ordinal);
    }
}
