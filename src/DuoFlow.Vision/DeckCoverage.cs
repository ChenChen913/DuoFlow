using System;
using System.Collections.Generic;

namespace DuoFlow.Vision;

/// <summary>Why a coverage estimate ended the way it did. Never an exception.</summary>
public enum DeckCoverageReason
{
    Ok = 0,

    /// <summary>Frame too small to say anything about.</summary>
    FrameTooSmall = 1,

    /// <summary>
    /// Whole frame is a featureless surface. Two very different physical
    /// situations produce this and the pixels cannot tell them apart: the lid
    /// has closed far enough that the camera is pressed against the deck, or
    /// the lens is capped / the room is dark. <see cref="DeckCoverage"/> reports
    /// coverage 1.0 because that is what the frame shows; deciding which
    /// situation it is requires history and belongs to
    /// <see cref="LidProgressTracker"/>.
    /// </summary>
    FlatFrame = 2,

    /// <summary>No region of the near brightness class reaches the bottom edge.</summary>
    NoNearSurface = 3,

    /// <summary>The frame has no two-population split to work with.</summary>
    Ambiguous = 4,

    /// <summary>A region was found but the evidence for it is too weak to use.</summary>
    LowConfidence = 5,
}

public sealed record DeckCoverageOptions
{
    public static DeckCoverageOptions Default { get; } = new();

    /// <summary>Long side of the working image.</summary>
    public int MaxWorkSide { get; init; } = 160;

    /// <summary>Below this a bottom-connected region is a speck, not a surface.</summary>
    public double MinAreaFraction { get; init; } = 0.02;

    /// <summary>Below this the frame carries no structure at all (see <see cref="DeckCoverageReason.FlatFrame"/>).</summary>
    public double FlatFrameStd { get; init; } = 2.0;

    /// <summary>
    /// Deliberately LOWER than <see cref="OtsuResult.FlatSceneSeparability"/>
    /// (0.35), which is the bar M3.2's screen detector uses.
    ///
    /// <para>
    /// The two detectors are asking different questions. M3.2 asks "is this a
    /// crisp object against a background" and a weak split means a weak answer.
    /// M3.3 asks only "which side of this split does the bottom of the frame
    /// sit on", and a deck that is nearly the same brightness as the desk
    /// behind it is still perfectly usable for that question - the split does
    /// not have to be pretty, it has to be in roughly the right place. Setting
    /// the screen detector's bar here would throw away the most common real
    /// desk (grey laptop on a grey desk).
    /// </para>
    /// </summary>
    public double MinSeparability { get; init; } = 0.20;

    /// <summary>Confidence a result needs before the tracker may consume it.</summary>
    public double UsableConfidence { get; init; } = 0.45;
}

/// <summary>
/// M3.3: how much of the frame is taken up by the near surface below the
/// camera.
///
/// <para>
/// <b>What this number is.</b> The camera sits in the lid and faces away from
/// the screen. Every degree the lid closes, the camera's optical axis pitches
/// down by a degree, and the surface "down there" climbs further up the frame.
/// <see cref="Coverage"/> is the fraction of the frame occupied by that
/// surface - which makes it a monotone proxy for how far the lid has closed.
/// </para>
/// <para>
/// <b>It deliberately does not try to identify the keyboard.</b> With the lid
/// wide open the surface at the bottom of the frame is the desk or the floor;
/// half way down it is the desk blending into the palm rest; at the end it is
/// the keyboard deck itself. All three are the same thing as far as the
/// measurement is concerned - the near surface under a downward-looking
/// camera - and asking "is this the keyboard" would need an answer the pixels
/// do not contain. The identity of the surface is irrelevant; its share of the
/// frame is not.
/// </para>
/// <para>
/// <b>Failure is a value, not an exception</b> (same contract as M3.2): every
/// rejection path returns the numbers that led there, because "the lid is
/// closed" and "the lens is capped" need different handling and look identical
/// in a single frame.
/// </para>
/// </summary>
public sealed record DeckCoverage
{
    public required DeckCoverageReason Reason { get; init; }

    /// <summary>Area of the bottom-connected near region as a fraction of the frame, 0..1.</summary>
    public double Coverage { get; init; }

    public double Confidence { get; init; }

    /// <summary>
    /// Topmost row of the near region, normalized (0 = frame top, 1 = frame
    /// bottom). 1.0 when there is no region. Reported for the trace: it is the
    /// quantity a human would actually eyeball.
    /// </summary>
    public double NearEdgeTop { get; init; } = 1.0;

    /// <summary>True when the near region covers essentially the whole frame.</summary>
    public bool FillsFrame { get; init; }

    /// <summary>True when the near region reaches the top edge - no far boundary above it.</summary>
    public bool TouchesTop { get; init; }

    public double NearMean { get; init; }

    public double FarMean { get; init; }

    /// <summary>Brightness gap between the near and far classes, in gray levels.</summary>
    public double ClassContrast => Math.Abs(NearMean - FarMean);

    public double SceneStd { get; init; }

    public double Separability { get; init; }

    public int Threshold { get; init; }

    public double UsableConfidence { get; init; } = DeckCoverageOptions.Default.UsableConfidence;

    /// <summary>True when <see cref="Coverage"/> and <see cref="Confidence"/> may be consumed.</summary>
    public bool IsUsable => Reason == DeckCoverageReason.Ok && Confidence >= UsableConfidence;

    /// <summary>One line for the trace, with no pixel content.</summary>
    public string Describe()
        => $"{Reason} coverage={Coverage:0.000} conf={Confidence:0.000} " +
           $"nearTop={NearEdgeTop:0.000} fills={(FillsFrame ? "yes" : "no")} " +
           $"touchesTop={(TouchesTop ? "yes" : "no")} " +
           $"near={NearMean:0.0} far={FarMean:0.0} std={SceneStd:0.00} " +
           $"otsu={Threshold} sep={Separability:0.000}";
}

/// <summary>
/// M3.3 stage 1: one camera frame in, one <see cref="DeckCoverage"/> out.
///
/// <para>
/// Shares M3.2's primitives (<see cref="GrayImage"/>, <see cref="OtsuThreshold"/>,
/// <see cref="ConnectedComponents"/>) but NOT its scorer. That is not an
/// oversight: M3.2's scoring contains
/// <c>HighTaper(areaFraction, 0.85, 0.95)</c>, which zeroes the confidence of
/// any region filling the frame - correct for "is that rectangle a display"
/// (a region with no boundary is not a display) and exactly backwards here,
/// where filling the frame is the strongest possible evidence that the lid is
/// down. The same number means opposite things in the two tasks, so the
/// scoring cannot be shared even though the plumbing can.
/// </para>
/// </summary>
public static class DeckCoverageEstimator
{
    public static DeckCoverage Estimate(GrayImage image, DeckCoverageOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= DeckCoverageOptions.Default;

        if (image.Width < 8 || image.Height < 8)
        {
            return Fail(DeckCoverageReason.FrameTooSmall, options);
        }

        GrayImage work = image.Downscaled(options.MaxWorkSide);
        int width = work.Width;
        int height = work.Height;
        int total = width * height;

        (double _, double std) = GrayStats.MeanAndStd(work.Pixels);

        if (std < options.FlatFrameStd)
        {
            // Nothing but a surface. Reporting coverage 1.0 is the honest read:
            // 100% of what the camera can see is one featureless thing.
            return new DeckCoverage
            {
                Reason = DeckCoverageReason.FlatFrame,
                Coverage = 1.0,
                Confidence = 1.0,
                NearEdgeTop = 0.0,
                FillsFrame = true,
                SceneStd = std,
                UsableConfidence = options.UsableConfidence,
            };
        }

        OtsuResult otsu = OtsuThreshold.Compute(work);
        if (otsu.Separability < options.MinSeparability)
        {
            return Fail(DeckCoverageReason.Ambiguous, options) with
            {
                Threshold = otsu.Threshold,
                Separability = otsu.Separability,
                SceneStd = std,
            };
        }

        int threshold = otsu.Threshold;
        ReadOnlySpan<byte> pixels = work.Pixels;

        // Which brightness class is the near surface? Ask the bottom row, and
        // ask for a majority rather than a single pixel: the near surface is
        // whatever occupies the bottom edge by definition, and one dark cable
        // lying there must not flip the answer for the whole frame.
        int bottomRow = (height - 1) * width;
        int brightOnBottom = 0;
        for (int x = 0; x < width; x++)
        {
            if (pixels[bottomRow + x] >= threshold)
            {
                brightOnBottom++;
            }
        }

        bool nearIsBright = brightOnBottom * 2 >= width;

        var mask = new bool[total];
        double nearSum = 0.0;
        int nearCount = 0;
        double farSum = 0.0;
        int farCount = 0;

        for (int i = 0; i < total; i++)
        {
            bool inNear = nearIsBright ? pixels[i] >= threshold : pixels[i] < threshold;
            mask[i] = inNear;
            if (inNear)
            {
                nearSum += pixels[i];
                nearCount++;
            }
            else
            {
                farSum += pixels[i];
                farCount++;
            }
        }

        double nearMean = nearCount > 0 ? nearSum / nearCount : 0.0;
        double farMean = farCount > 0 ? farSum / farCount : nearMean;

        // The near surface must REACH THE BOTTOM EDGE. Without this, a bright
        // window or a monitor on the wall is tracked just as happily as the
        // desk - and those do not move when the lid does, so the progress
        // signal would be flat while the lid closes.
        int minArea = Math.Max(16, (int)(total * options.MinAreaFraction));
        Blob? best = null;
        foreach (Blob blob in ConnectedComponents.Label(mask, width, height))
        {
            if (blob.MaxY != height - 1 || blob.Area < minArea)
            {
                continue;
            }

            if (best is null || blob.Area > best.Area)
            {
                best = blob;
            }
        }

        if (best is null)
        {
            return Fail(DeckCoverageReason.NoNearSurface, options) with
            {
                Threshold = threshold,
                Separability = otsu.Separability,
                SceneStd = std,
                NearMean = nearMean,
                FarMean = farMean,
            };
        }

        double coverage = (double)best.Area / total;

        // Three multiplicative factors, each answering "can I trust this number
        // at all". Multiplied, not weighted: M3.2 learned three times over that
        // an additive score lets a candidate fail one test and still pass.
        double split = ScoreShapes.Window(otsu.Separability, 0.15, 0.40, 1.0, 1.0);
        double size = ScoreShapes.Window(coverage, 0.01, 0.05, 1.0, 1.0);
        double contrast = ScoreShapes.Window(Math.Abs(nearMean - farMean), 3.0, 12.0, 1000.0, 1000.0);

        double confidence = split * size * contrast;

        // A region that reaches the top edge has no far boundary left, so
        // Coverage stops meaning "how much surface is below the boundary" and
        // starts meaning "how much of the frame is on one side of a brightness
        // split". That reading is weaker evidence, but it is NOT absent
        // evidence: a near surface that has climbed past the top of the frame
        // is exactly what a nearly-closed lid looks like, and rejecting it
        // throws away the last and strongest third of the travel.
        //
        // Recorded rather than acted on. The tracker and M3.4's smoothing can
        // decide what to do with a frame whose far boundary has left the
        // frame; a single frame cannot.
        bool touchesTop = best.MinY == 0;
        bool fills = coverage >= 0.95;

        var result = new DeckCoverage
        {
            Reason = DeckCoverageReason.Ok,
            Coverage = coverage,
            Confidence = confidence,
            NearEdgeTop = (double)best.MinY / height,
            FillsFrame = fills,
            TouchesTop = touchesTop,
            NearMean = nearMean,
            FarMean = farMean,
            SceneStd = std,
            Separability = otsu.Separability,
            Threshold = threshold,
            UsableConfidence = options.UsableConfidence,
        };

        return confidence < options.UsableConfidence
            ? result with { Reason = DeckCoverageReason.LowConfidence }
            : result;
    }

    private static DeckCoverage Fail(DeckCoverageReason reason, DeckCoverageOptions options)
        => new() { Reason = reason, UsableConfidence = options.UsableConfidence };
}
