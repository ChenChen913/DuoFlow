using System;
using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

public class LidProgressTrackerTests
{
    private static DeckCoverage Ok(double coverage, double confidence = 0.9) => new()
    {
        Reason = DeckCoverageReason.Ok,
        Coverage = coverage,
        Confidence = confidence,
        UsableConfidence = 0.45,
    };

    private static DeckCoverage Blank() => new()
    {
        Reason = DeckCoverageReason.FlatFrame,
        Coverage = 1.0,
        Confidence = 1.0,
        FillsFrame = true,
        UsableConfidence = 0.45,
    };

    private static DeckCoverage Nothing() => new()
    {
        Reason = DeckCoverageReason.NoNearSurface,
        UsableConfidence = 0.45,
    };

    [Fact]
    public void OpenCoverageMapsToZero()
    {
        var tracker = new LidProgressTracker();

        LidProgressEstimate estimate = tracker.Update(Ok(0.20));

        Assert.Equal(LidProgressReason.Measured, estimate.Reason);
        Assert.Equal(0.0, estimate.Progress, 9);
        Assert.True(estimate.HasMeasurement);
    }

    [Fact]
    public void ClosedCoverageMapsToOne()
    {
        var tracker = new LidProgressTracker();

        LidProgressEstimate estimate = tracker.Update(Ok(0.95));

        Assert.Equal(1.0, estimate.Progress, 9);
        Assert.True(estimate.IsClosed);
    }

    [Fact]
    public void MidCoverageMapsHalfway()
    {
        var tracker = new LidProgressTracker();

        // Defaults are 0.20 -> 0 and 0.95 -> 1, so the midpoint is 0.575.
        LidProgressEstimate estimate = tracker.Update(Ok(0.575));

        Assert.Equal(0.5, estimate.Progress, 6);
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(0.10)]
    [InlineData(0.199)]
    public void CoverageBelowTheOpenKnotClampsToZero(double coverage)
    {
        var tracker = new LidProgressTracker();

        Assert.Equal(0.0, tracker.Update(Ok(coverage)).Progress, 9);
    }

    [Theory]
    [InlineData(0.96)]
    [InlineData(1.00)]
    public void CoverageAboveTheClosedKnotClampsToOne(double coverage)
    {
        var tracker = new LidProgressTracker();

        Assert.Equal(1.0, tracker.Update(Ok(coverage)).Progress, 9);
    }

    /// <summary>
    /// The property the renderer actually depends on: as the lid closes, the
    /// progress the animation consumes must never go backwards on its own.
    /// </summary>
    [Fact]
    public void MonotoneCoverageGivesMonotoneProgress()
    {
        var tracker = new LidProgressTracker();
        double previous = -1.0;

        for (int i = 0; i <= 30; i++)
        {
            double coverage = 0.20 + (0.75 * i / 30.0);
            LidProgressEstimate estimate = tracker.Update(Ok(coverage));

            Assert.True(estimate.Progress >= previous, $"progress went backwards at step {i}");
            previous = estimate.Progress;
        }

        Assert.Equal(1.0, previous, 6);
    }

    /// <summary>
    /// A blank frame at the END of a closing motion means the camera is against
    /// the deck. This is the one case where "the frame went black" is the
    /// strongest possible evidence, not a failure.
    /// </summary>
    [Fact]
    public void BlankFrameAfterHighProgressConfirmsClosed()
    {
        var tracker = new LidProgressTracker();
        tracker.Update(Ok(0.90));

        LidProgressEstimate estimate = tracker.Update(Blank());

        Assert.Equal(LidProgressReason.ClosedConfirmed, estimate.Reason);
        Assert.Equal(1.0, estimate.Progress, 9);
        Assert.Equal(1.0, estimate.Confidence, 9);
        Assert.True(estimate.IsClosed);
        Assert.True(tracker.IsClosedLatched);
    }

    /// <summary>
    /// The same blank frame at START-UP must not be read as "closed". A capped
    /// lens, a dark room and a closed privacy shutter all produce it, and
    /// treating it as closed would play a full closing animation on every boot.
    /// </summary>
    [Fact]
    public void BlankFrameAtStartupIsNoMeasurement()
    {
        var tracker = new LidProgressTracker();

        LidProgressEstimate estimate = tracker.Update(Blank());

        Assert.Equal(LidProgressReason.NoMeasurement, estimate.Reason);
        Assert.Equal(0.0, estimate.Progress, 9);
        Assert.Equal(0.0, estimate.Confidence, 9);
        Assert.False(estimate.IsClosed);
        Assert.False(tracker.IsClosedLatched);
    }

    /// <summary>
    /// Between "clearly open" and "clearly closed" the blank frame is
    /// ambiguous, and the safe answer is to admit that rather than guess.
    /// </summary>
    [Fact]
    public void BlankFrameBelowTheConfirmThresholdDoesNotConfirm()
    {
        var tracker = new LidProgressTracker();
        LidProgressEstimate before = tracker.Update(Ok(0.55));   // ~0.467

        LidProgressEstimate estimate = tracker.Update(Blank());

        Assert.Equal(LidProgressReason.NoMeasurement, estimate.Reason);
        Assert.Equal(before.Progress, estimate.Progress, 9);
        Assert.False(tracker.IsClosedLatched);
    }

    [Fact]
    public void NoMeasurementFreezesTheLastProgress()
    {
        var tracker = new LidProgressTracker();
        LidProgressEstimate before = tracker.Update(Ok(0.65));

        LidProgressEstimate estimate = tracker.Update(Nothing());

        Assert.Equal(LidProgressReason.NoMeasurement, estimate.Reason);
        Assert.Equal(before.Progress, estimate.Progress, 9);
        Assert.Equal(0.0, estimate.Confidence, 9);
        Assert.False(estimate.HasMeasurement);
    }

    /// <summary>
    /// A sample the estimator itself did not trust must not move the progress -
    /// otherwise a "LowConfidence" result would still be consumed, and the whole
    /// point of reporting the confidence would be lost.
    /// </summary>
    [Fact]
    public void LowConfidenceSampleIsIgnored()
    {
        var tracker = new LidProgressTracker();
        LidProgressEstimate before = tracker.Update(Ok(0.65));

        LidProgressEstimate estimate = tracker.Update(Ok(0.95, confidence: 0.20));

        Assert.Equal(LidProgressReason.NoMeasurement, estimate.Reason);
        Assert.Equal(before.Progress, estimate.Progress, 9);
    }

    /// <summary>
    /// After a confirmed close, a real measurement showing the lid open again
    /// must clear the latch. Otherwise "closed" would be sticky forever: the
    /// user reopens the lid, caps the lens, and the provider keeps reporting a
    /// closed lid.
    /// </summary>
    [Fact]
    public void ReopeningClearsTheClosedLatch()
    {
        var tracker = new LidProgressTracker();
        tracker.Update(Ok(0.90));
        tracker.Update(Blank());
        Assert.True(tracker.IsClosedLatched);

        LidProgressEstimate reopened = tracker.Update(Ok(0.25));

        Assert.Equal(LidProgressReason.Measured, reopened.Reason);
        Assert.False(tracker.IsClosedLatched);
        Assert.False(reopened.IsClosed);

        // And now a blank frame is ambiguous again, not "closed".
        LidProgressEstimate after = tracker.Update(Blank());
        Assert.Equal(LidProgressReason.NoMeasurement, after.Reason);
    }

    /// <summary>A custom calibration must actually be used, not silently ignored.</summary>
    [Fact]
    public void CustomCalibrationIsHonoured()
    {
        var calibration = new LidProgressCalibration { OpenCoverage = 0.40, ClosedCoverage = 0.80 };
        var tracker = new LidProgressTracker(calibration);

        Assert.Equal(0.0, tracker.Update(Ok(0.40)).Progress, 9);
        Assert.Equal(0.5, tracker.Update(Ok(0.60)).Progress, 9);
        Assert.Equal(1.0, tracker.Update(Ok(0.80)).Progress, 9);
    }

    /// <summary>
    /// An inverted calibration would divide by a negative span and turn the
    /// progress curve upside down - a silent, plausible-looking disaster. It is
    /// rejected at construction instead.
    /// </summary>
    [Theory]
    [InlineData(0.90, 0.20)]
    [InlineData(0.50, 0.50)]
    [InlineData(double.NaN, 0.95)]
    public void InvalidCalibrationIsRejected(double open, double closed)
    {
        var calibration = new LidProgressCalibration { OpenCoverage = open, ClosedCoverage = closed };

        Assert.Throws<ArgumentOutOfRangeException>(() => new LidProgressTracker(calibration));
    }

    [Fact]
    public void InvalidConfirmThresholdIsRejected()
    {
        var calibration = new LidProgressCalibration { ClosedConfirmProgress = 1.5 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new LidProgressTracker(calibration));
    }

    [Fact]
    public void DescribeIsSafeAndInformative()
    {
        var tracker = new LidProgressTracker();

        string text = tracker.Update(Ok(0.6)).Describe();

        Assert.Contains("Measured", text, StringComparison.Ordinal);
        Assert.Contains("progress=", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Measured on the development laptop: with the placeholder open knot
    /// (0.20) a fully open lid reads 0.513 coverage and therefore progress
    /// 0.417 - the app would launch with the desktop already 42% folded.
    ///
    /// The fix is not a better constant (no constant survives a different desk,
    /// room or laptop height); it is to take the first usable sample as the
    /// definition of "open". A desktop app starts with the lid open by
    /// necessity - otherwise the user could not have launched it.
    /// </summary>
    [Fact]
    public void FirstUsableSampleBecomesTheOpenBaseline()
    {
        var calibration = new LidProgressCalibration { LearnOpenBaseline = true };
        var tracker = new LidProgressTracker(calibration);

        LidProgressEstimate first = tracker.Update(Ok(0.513));

        Assert.Equal(0.0, first.Progress, 9);
        Assert.True(tracker.HasLearnedBaseline);
        Assert.Equal(0.513, tracker.OpenBaseline, 6);

        // 0.60 is 0.087 above the learned baseline, over a span of 0.437.
        LidProgressEstimate second = tracker.Update(Ok(0.60));

        Assert.Equal(0.087 / 0.437, second.Progress, 6);
    }

    /// <summary>
    /// A blank frame must never teach the tracker that "open" is coverage 1.0 -
    /// a capped lens at start-up would otherwise peg the baseline at the closed
    /// knot and the provider would never report any movement again.
    /// </summary>
    [Fact]
    public void BlankFrameCannotBecomeTheBaseline()
    {
        var calibration = new LidProgressCalibration { LearnOpenBaseline = true };
        var tracker = new LidProgressTracker(calibration);

        LidProgressEstimate blank = tracker.Update(Blank());

        Assert.Equal(LidProgressReason.NoMeasurement, blank.Reason);
        Assert.False(tracker.HasLearnedBaseline);

        LidProgressEstimate measured = tracker.Update(Ok(0.40));

        Assert.True(tracker.HasLearnedBaseline);
        Assert.Equal(0.40, tracker.OpenBaseline, 6);
        Assert.Equal(0.0, measured.Progress, 9);
    }

    [Fact]
    public void BaselineLearningIsOffByDefault()
    {
        var tracker = new LidProgressTracker();

        LidProgressEstimate estimate = tracker.Update(Ok(0.513));

        Assert.False(tracker.HasLearnedBaseline);
        Assert.Equal(0.20, tracker.OpenBaseline, 6);
        Assert.Equal((0.513 - 0.20) / 0.75, estimate.Progress, 6);
    }

    /// <summary>
    /// A pathological baseline (learned while the lid was already shut) leaves
    /// no span to map onto. The answer must be "no evidence of movement", not a
    /// divide-by-zero and not a claim that the lid is closed.
    /// </summary>
    [Fact]
    public void DegenerateBaselineYieldsZeroInsteadOfADivideByZero()
    {
        var calibration = new LidProgressCalibration { LearnOpenBaseline = true };
        var tracker = new LidProgressTracker(calibration);

        LidProgressEstimate first = tracker.Update(Ok(0.95));
        LidProgressEstimate second = tracker.Update(Ok(0.99));

        Assert.Equal(0.0, first.Progress, 9);
        Assert.Equal(0.0, second.Progress, 9);
        Assert.True(double.IsFinite(second.Progress));
    }
}
