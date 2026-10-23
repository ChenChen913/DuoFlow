using System;
using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M3.2: does the detector find the laptop display, and does it decline
/// honestly when there is nothing to find?
///
/// Every scene is built from normalized corners so one spec can be rendered at
/// any resolution - which is what makes the scale-invariance test mean
/// something. Nothing here uses a real camera: the point of putting detection
/// in the pure-net8.0 assembly is that these tests run in cloud CI, on a
/// machine with no camera at all.
/// </summary>
public class ScreenDetectorTests
{
    private static readonly QuadPoint[] ScreenCorners =
    {
        new(0.15, 0.20),
        new(0.85, 0.20),
        new(0.85, 0.75),
        new(0.15, 0.75),
    };

    private static SceneSpec BrightScreen(int noise = 0) => new()
    {
        Corners = ScreenCorners,
        Background = 35,
        Screen = 210,
        Noise = noise,
    };

    private static GrayImage Render(SceneSpec spec, int width = 640, int height = 360)
        => SyntheticScene.Render(width, height, spec);

    // -----------------------------------------------------------------
    // The happy path
    // -----------------------------------------------------------------

    [Fact]
    public void FindsABrightScreenOnADarkBackground()
    {
        ScreenDetection result = ScreenDetector.Detect(Render(BrightScreen()));

        Assert.Equal(ScreenDetectionReason.Ok, result.Reason);
        Assert.NotNull(result.Quad);
        Assert.True(result.IsUsable, result.Describe());
        Assert.True(result.Confidence >= 0.60, result.Describe());

        double error = QuadAssert.MaxCornerError(
            result.Quad!, QuadAssert.FromCorners(ScreenCorners));
        Assert.True(error <= 0.03, $"corner error {error:0.000} - {result.Describe()}");
    }

    [Fact]
    public void ReportsTheBrightPolarity()
    {
        ScreenDetection result = ScreenDetector.Detect(Render(BrightScreen()));
        Assert.Equal(ScreenPolarity.Bright, result.Polarity);
    }

    [Fact]
    public void FindsADarkScreenOnABrightBackground()
    {
        // A display in a bright room is often darker than what is behind it.
        // A detector that only looks for "the brightest rectangle" would work
        // at night on the developer's desk and fail at noon.
        var spec = new SceneSpec
        {
            Corners = ScreenCorners,
            Background = 200,
            Screen = 40,
        };

        ScreenDetection result = ScreenDetector.Detect(Render(spec));

        Assert.True(result.IsUsable, result.Describe());
        Assert.Equal(ScreenPolarity.Dark, result.Polarity);
        Assert.True(QuadAssert.MaxCornerError(result.Quad!, QuadAssert.FromCorners(ScreenCorners)) <= 0.03,
            result.Describe());
    }

    [Fact]
    public void FindsAForeshortenedTrapezoid()
    {
        // Lid partly closed: narrow at the top, wide at the bottom. This is the
        // shape a bounding box would destroy and the whole reason M3.2 returns
        // a general quad.
        QuadPoint[] corners =
        {
            new(0.25, 0.18),
            new(0.75, 0.18),
            new(0.88, 0.80),
            new(0.12, 0.80),
        };

        var spec = new SceneSpec { Corners = corners, Background = 35, Screen = 210 };
        ScreenDetection result = ScreenDetector.Detect(Render(spec));

        Assert.True(result.IsUsable, result.Describe());
        // The signature of foreshortening is the top edge being shorter than
        // the bottom one. Asserting on that rather than on the aspect number
        // keeps the test independent of how aspect is defined.
        NormalizedQuad quad = result.Quad!;
        Assert.True(
            (quad.TopRight.X - quad.TopLeft.X) < (quad.BottomRight.X - quad.BottomLeft.X),
            result.Describe());
        Assert.True(QuadAssert.MaxCornerError(quad, QuadAssert.FromCorners(corners)) <= 0.05,
            result.Describe());
    }

    [Fact]
    public void PhysicallySquareRegion_InAWideFrame_IsStillUsable()
    {
        // 200x200 pixels inside a 640x360 frame. In normalized units that is
        // 0.3125 wide by 0.5556 tall - an aspect of 0.56, which the veto would
        // reject if it compared raw normalized distances. It is a perfectly
        // ordinary screen shape and must be accepted.
        QuadPoint[] corners =
        {
            new(0.3125, 0.2222),
            new(0.6250, 0.2222),
            new(0.6250, 0.7778),
            new(0.3125, 0.7778),
        };

        var spec = new SceneSpec { Corners = corners, Background = 35, Screen = 210 };
        ScreenDetection result = ScreenDetector.Detect(Render(spec));

        Assert.True(result.IsUsable, result.Describe());
        Assert.Equal(1.0, result.AspectRatio, 2);
    }

    [Fact]
    public void SurvivesSensorNoise()
    {
        ScreenDetection clean = ScreenDetector.Detect(Render(BrightScreen()));
        ScreenDetection noisy = ScreenDetector.Detect(Render(BrightScreen(noise: 6)));

        Assert.True(noisy.IsUsable, noisy.Describe());
        // Noise costs a little confidence, but must not change the answer.
        Assert.True(noisy.Confidence > clean.Confidence - 0.20, noisy.Describe());
        Assert.True(QuadAssert.MaxCornerError(noisy.Quad!, clean.Quad!) <= 0.04, noisy.Describe());
    }

    [Fact]
    public void PrefersTheScreenOverASmallerBrightWindow()
    {
        var spec = new SceneSpec
        {
            Corners = ScreenCorners,
            Background = 35,
            Screen = 210,
            Extras = new[]
            {
                (new QuadPoint[] { new(0.02, 0.02), new(0.10, 0.02), new(0.10, 0.10), new(0.02, 0.10) }, (byte)210),
            },
        };

        ScreenDetection result = ScreenDetector.Detect(Render(spec));

        Assert.True(result.IsUsable, result.Describe());
        // The screen occupies ~0.385 of the frame; the window ~0.006.
        Assert.True(result.AreaFraction > 0.20, result.Describe());
    }

    [Fact]
    public void CornerOrderIsTopLeftThenClockwise()
    {
        ScreenDetection result = ScreenDetector.Detect(Render(BrightScreen()));
        NormalizedQuad quad = result.Quad!;

        Assert.True(quad.SignedArea > 0.0, "corners must wind clockwise on screen");
        // TopLeft really is the most top-left of the four.
        double anchor = quad.TopLeft.X + quad.TopLeft.Y;
        for (int i = 1; i < 4; i++)
        {
            Assert.True(quad[i].X + quad[i].Y >= anchor - 1e-9);
        }
    }

    [Fact]
    public void QuadStaysInsideTheUnitSquare()
    {
        ScreenDetection result = ScreenDetector.Detect(Render(BrightScreen()));
        for (int i = 0; i < 4; i++)
        {
            Assert.InRange(result.Quad![i].X, 0.0, 1.0);
            Assert.InRange(result.Quad[i].Y, 0.0, 1.0);
        }
    }

    // -----------------------------------------------------------------
    // Scale invariance - the property that keeps "no pixel coordinates"
    // from being a comment instead of a rule.
    // -----------------------------------------------------------------

    [Fact]
    public void SameSceneAtDifferentResolutions_GivesTheSameQuad()
    {
        var spec = BrightScreen();
        int[][] sizes = { new[] { 480, 270 }, new[] { 960, 540 }, new[] { 1280, 720 } };

        NormalizedQuad?[] quads = new NormalizedQuad?[sizes.Length];
        for (int i = 0; i < sizes.Length; i++)
        {
            ScreenDetection result = ScreenDetector.Detect(Render(spec, sizes[i][0], sizes[i][1]));
            Assert.True(result.IsUsable, $"{sizes[i][0]}x{sizes[i][1]}: {result.Describe()}");
            quads[i] = result.Quad;
        }

        for (int i = 1; i < quads.Length; i++)
        {
            double error = QuadAssert.MaxCornerError(quads[0]!, quads[i]!);
            Assert.True(error <= 0.015,
                $"{sizes[0][0]}x{sizes[0][1]} vs {sizes[i][0]}x{sizes[i][1]}: error {error:0.0000}");
        }
    }

    [Fact]
    public void NonWideAspectFrame_StillWorks()
    {
        // 4:3 and 16:10 cameras exist; the geometry must not assume 16:9.
        ScreenDetection result = ScreenDetector.Detect(Render(BrightScreen(), 640, 480));

        Assert.True(result.IsUsable, result.Describe());
        Assert.True(QuadAssert.MaxCornerError(result.Quad!, QuadAssert.FromCorners(ScreenCorners)) <= 0.04,
            result.Describe());
    }

    // -----------------------------------------------------------------
    // Declining honestly - every one of these must be a value, not a throw
    // -----------------------------------------------------------------

    [Fact]
    public void FlatFrame_ReportsFlatScene()
    {
        var pixels = new byte[320 * 240];
        Array.Fill(pixels, (byte)90);

        ScreenDetection result = ScreenDetector.Detect(new GrayImage(320, 240, pixels));

        Assert.Equal(ScreenDetectionReason.FlatScene, result.Reason);
        Assert.Null(result.Quad);
        Assert.False(result.IsUsable);
        Assert.Equal(0.0, result.Separability, 6);
    }

    [Fact]
    public void BlackFrame_ReportsFlatScene()
    {
        // The capped-lens case, measured on the development laptop: every byte
        // 15. It must be reported as "nothing to see", not as a crash and not
        // as a low-confidence quad.
        var pixels = new byte[160 * 90];
        Array.Fill(pixels, (byte)15);

        ScreenDetection result = ScreenDetector.Detect(new GrayImage(160, 90, pixels));

        Assert.Equal(ScreenDetectionReason.FlatScene, result.Reason);
        Assert.False(result.IsUsable);
    }

    [Fact]
    public void PureNoise_IsNotUsable()
    {
        var rng = new Random(4242);
        var pixels = new byte[320 * 240];
        rng.NextBytes(pixels);

        ScreenDetection result = ScreenDetector.Detect(new GrayImage(320, 240, pixels));

        Assert.False(result.IsUsable, result.Describe());
    }

    [Fact]
    public void ThinBrightLine_IsNotUsable()
    {
        // A perfect convex rectangle with sharp edges and plenty of contrast -
        // and not a screen. Only the aspect ratio can tell the difference, so
        // this is the test that proves aspect can veto rather than nudge.
        QuadPoint[] corners =
        {
            new(0.05, 0.48),
            new(0.95, 0.48),
            new(0.95, 0.52),
            new(0.05, 0.52),
        };

        var spec = new SceneSpec { Corners = corners, Background = 35, Screen = 210 };
        ScreenDetection result = ScreenDetector.Detect(Render(spec));

        Assert.False(result.IsUsable, result.Describe());
    }

    [Fact]
    public void LowContrastScreen_ScoresBelowAHighContrastOne()
    {
        var faint = new SceneSpec { Corners = ScreenCorners, Background = 90, Screen = 110 };
        var strong = BrightScreen();

        double faintConfidence = ScreenDetector.Detect(Render(faint)).Confidence;
        double strongConfidence = ScreenDetector.Detect(Render(strong)).Confidence;

        Assert.True(faintConfidence < strongConfidence,
            $"faint {faintConfidence:0.000} should be below strong {strongConfidence:0.000}");
    }

    [Fact]
    public void TinyFrames_ReportFrameTooSmall()
    {
        ScreenDetection result = ScreenDetector.Detect(new GrayImage(8, 8, new byte[64]));
        Assert.Equal(ScreenDetectionReason.FrameTooSmall, result.Reason);
        Assert.False(result.IsUsable);
    }

    [Theory]
    [InlineData(16, 16)]
    [InlineData(17, 16)]
    [InlineData(160, 90)]
    [InlineData(1, 400)]
    public void DegenerateFrames_NeverThrow(int width, int height)
    {
        var pixels = new byte[width * height];
        var rng = new Random(width * 31 + height);
        rng.NextBytes(pixels);

        Exception? thrown = Record.Exception(
            () => ScreenDetector.Detect(new GrayImage(width, height, pixels)));

        Assert.Null(thrown);
    }

    [Fact]
    public void NullImage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ScreenDetector.Detect(null!));
    }

    [Fact]
    public void Options_CanTightenTheBar()
    {
        var faint = new SceneSpec { Corners = ScreenCorners, Background = 90, Screen = 110 };
        ScreenDetection lenient = ScreenDetector.Detect(Render(faint));
        ScreenDetection strict = ScreenDetector.Detect(
            Render(faint), new ScreenDetectionOptions { UsableConfidence = 0.99 });

        // Same evidence, different bar: the quad is still found, it just is
        // not trusted enough to feed M3.3.
        Assert.NotNull(strict.Quad);
        Assert.Equal(0.99, strict.UsableConfidence);
        Assert.False(strict.IsUsable);
        Assert.Equal(lenient.Confidence, strict.Confidence, 6);
    }

    [Fact]
    public void CappedLensWithSensorNoise_IsNotUsable()
    {
        // The real-machine case, reproduced: a capped lens does not give a
        // perfect zero, it gives the black floor plus a little sensor
        // variation (measured: every byte 15, contrast delta 0 on the sampled
        // grid). Before the flat-frame guard and the contrast veto this was
        // reported as a usable screen in 24 of 30 frames at up to 0.57
        // confidence - the quad being the whole frame.
        var rng = new Random(15);
        var pixels = new byte[640 * 360];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(15 + (rng.Next(0, 3) - 1));   // 14..16
        }

        ScreenDetection result = ScreenDetector.Detect(new GrayImage(640, 360, pixels));

        Assert.False(result.IsUsable, result.Describe());
        Assert.True(result.SceneStd < ScreenDetector.FlatFrameStd, result.Describe());
    }

    [Fact]
    public void NoiseAboveTheRawFlatThreshold_IsStillRejected()
    {
        // Noisier than the flat guard would allow at full resolution - but
        // SceneStd is measured on the DOWNSCALED frame, and a 4x4 box average
        // divides pure noise by ~4. That is the right thing to measure: it
        // reports structure, not noise. A frame whose only content is
        // high-frequency noise has no screen in it whichever guard catches it.
        var rng = new Random(77);
        var pixels = new byte[640 * 360];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)Math.Clamp(20 + rng.Next(-6, 7), 0, 255);
        }

        ScreenDetection result = ScreenDetector.Detect(new GrayImage(640, 360, pixels));

        Assert.False(result.IsUsable, result.Describe());
    }

    [Fact]
    public void LowContrastScreen_IsRejectedByTheContrastVeto()
    {
        // Structured (well above the flat guard) but only 8 gray levels of
        // boundary contrast. A boundary that faint is not a boundary, and this
        // is the case the flat guard cannot catch - it needs the veto.
        var spec = new SceneSpec { Corners = ScreenCorners, Background = 100, Screen = 108 };
        ScreenDetection result = ScreenDetector.Detect(Render(spec));

        Assert.False(result.IsUsable, result.Describe());
        Assert.True(result.SceneStd >= ScreenDetector.FlatFrameStd, "scene has structure, so this is the veto's case");
        Assert.True(result.EdgeContrast < 12.0, result.Describe());
    }

    [Fact]
    public void NeverCallsTheWholeFrameAScreen()
    {
        // The inverted polarity of a thin-line scene makes the BACKGROUND the
        // foreground region - 98% of the frame, a perfect rectangle, and not a
        // screen. A candidate that fills the view has no boundary to measure,
        // so it must be vetoed rather than merely penalised.
        QuadPoint[] corners =
        {
            new(0.05, 0.48),
            new(0.95, 0.48),
            new(0.95, 0.52),
            new(0.05, 0.52),
        };

        ScreenDetection result = ScreenDetector.Detect(
            Render(new SceneSpec { Corners = corners, Background = 35, Screen = 210 }));

        Assert.False(result.IsUsable, result.Describe());
        Assert.True(result.AreaFraction < 0.85 || result.Confidence == 0.0, result.Describe());
    }

    [Fact]
    public void SmallerWorkingSize_StillFindsTheScreen()
    {
        var small = new ScreenDetectionOptions { MaxWorkSide = 64 };
        ScreenDetection result = ScreenDetector.Detect(Render(BrightScreen()), small);

        Assert.True(result.IsUsable, result.Describe());
        Assert.True(QuadAssert.MaxCornerError(result.Quad!, QuadAssert.FromCorners(ScreenCorners)) <= 0.05,
            result.Describe());
    }

    [Fact]
    public void InteriorScreen_IsNotReportedAsClipped()
    {
        ScreenDetection result = ScreenDetector.Detect(Render(BrightScreen()));
        Assert.False(result.TouchesFrameEdge, result.Describe());
    }

    [Fact]
    public void ScreenRunningOffTheFrame_IsReportedAsClipped()
    {
        // A region cut by the frame border has its real corners outside the
        // picture, so the fitted quad is a truncation. Detection still works;
        // the flag is what stops M3.3 from trusting the angles.
        QuadPoint[] corners =
        {
            new(0.00, 0.10),
            new(0.70, 0.10),
            new(0.70, 0.80),
            new(0.00, 0.80),
        };

        var spec = new SceneSpec { Corners = corners, Background = 35, Screen = 210 };
        ScreenDetection result = ScreenDetector.Detect(Render(spec));

        Assert.True(result.IsUsable, result.Describe());
        Assert.True(result.TouchesFrameEdge, result.Describe());
    }

    [Fact]
    public void Describe_IsOneLineWithoutPixelContent()
    {
        string text = ScreenDetector.Detect(Render(BrightScreen())).Describe();

        Assert.DoesNotContain("\n", text);
        Assert.Contains("conf=", text);
        Assert.Contains("quad=", text);
    }
}
