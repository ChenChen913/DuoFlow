using System;
using System.Collections.Generic;

namespace DuoFlow.Vision;

/// <summary>Why a detection ended the way it did. Never an exception.</summary>
public enum ScreenDetectionReason
{
    Ok = 0,
    FrameTooSmall = 1,
    FlatScene = 2,
    NoForeground = 3,
    DegenerateQuad = 4,
    LowConfidence = 5,
}

/// <summary>
/// Which way round the screen stood out from its surroundings.
///
/// Reported rather than assumed: a display in a bright room can easily be
/// DARKER than what is behind it, and a detector that only ever looks for
/// "brightest rectangle" would work on the developer's desk at night and fail
/// on the same laptop at noon.
/// </summary>
public enum ScreenPolarity
{
    Unknown = 0,
    Bright = 1,
    Dark = 2,
}

public sealed record ScreenDetectionOptions
{
    public static ScreenDetectionOptions Default { get; } = new();

    /// <summary>Long side of the working image. Detection runs on this, not the full frame.</summary>
    public int MaxWorkSide { get; init; } = 160;

    /// <summary>Below this the blob is noise: a reflection or a bezel highlight.</summary>
    public double MinAreaFraction { get; init; } = 0.02;

    /// <summary>
    /// Confidence a result needs before M3.3 may use it. Below this the caller
    /// must degrade (HARDWARE §4.1 Level 1), not "try harder".
    /// </summary>
    public double UsableConfidence { get; init; } = 0.45;
}

/// <summary>
/// The outcome of one detection pass. Every field is diagnostic: when the
/// answer is "I could not find the screen", the numbers here are what says
/// whether the room was dark, the blob was the wrong shape, or the contrast
/// was absent.
/// </summary>
public sealed record ScreenDetection
{
    public required ScreenDetectionReason Reason { get; init; }

    public NormalizedQuad? Quad { get; init; }

    public double Confidence { get; init; }

    public ScreenPolarity Polarity { get; init; }

    /// <summary>Quad area as a fraction of the whole frame.</summary>
    public double AreaFraction { get; init; }

    /// <summary>Blob area / convex hull area. ~1 for a screen, lower for a weird shape.</summary>
    public double Rectangularity { get; init; }

    /// <summary>Blob area / quad area. How well the quad explains the blob.</summary>
    public double Fill { get; init; }

    /// <summary>|mean inside - mean in a ring just outside|, in gray levels.</summary>
    public double EdgeContrast { get; init; }

    public double AspectRatio { get; init; }

    /// <summary>
    /// True when a corner sits on the frame border, meaning the region is
    /// CLIPPED: its real corners are outside the picture and the fitted quad
    /// is a truncation of it.
    ///
    /// Reported, not gated. A clipped quad still carries usable geometry for
    /// "is the lid moving", but its aspect and its corner angles are wrong by
    /// an unknown amount, so M3.3 must not treat it like a fully visible one.
    /// Cheap to compute and expensive to discover later.
    /// </summary>
    public bool TouchesFrameEdge { get; init; }

    /// <summary>
    /// Standard deviation of the whole working frame, in gray levels.
    ///
    /// The single most useful number for explaining a failure: a capped lens
    /// or a dark room produces ~0.5, a real desk scene produces 30+. Reported
    /// on every outcome, including successes, because "it found something in a
    /// frame with std 0.4" is exactly the false positive worth noticing.
    /// </summary>
    public double SceneStd { get; init; }

    public int Threshold { get; init; }

    public double Separability { get; init; }

    public double UsableConfidence { get; init; } = ScreenDetectionOptions.Default.UsableConfidence;

    /// <summary>True when M3.3 may consume <see cref="Quad"/>.</summary>
    public bool IsUsable => Quad is not null && Confidence >= UsableConfidence;

    /// <summary>One line for the trace, with no pixel content.</summary>
    public string Describe()
    {
        string quad = Quad is null
            ? "quad=none"
            : $"quad=({Quad.TopLeft.X:0.000},{Quad.TopLeft.Y:0.000}) " +
              $"({Quad.TopRight.X:0.000},{Quad.TopRight.Y:0.000}) " +
              $"({Quad.BottomRight.X:0.000},{Quad.BottomRight.Y:0.000}) " +
              $"({Quad.BottomLeft.X:0.000},{Quad.BottomLeft.Y:0.000})";

        return $"{Reason} conf={Confidence:0.000} polarity={Polarity} " +
               $"area={AreaFraction:0.000} rect={Rectangularity:0.000} fill={Fill:0.000} " +
               $"edge={EdgeContrast:0.0} aspect={AspectRatio:0.00} std={SceneStd:0.00} " +
               $"clipped={(TouchesFrameEdge ? "yes" : "no")} " +
               $"otsu={Threshold} sep={Separability:0.000} {quad}";
    }
}

/// <summary>
/// M3.2: finds the laptop display in a camera frame.
///
/// <para>
/// <b>It does not know where the camera is, and it never asks.</b>
/// <c>HARDWARE_COMPATIBILITY</c> §2.5 is explicit: top-centre, top-left,
/// notched, IR+Rgb - all of them must reach the same algorithm, so the
/// algorithm finds the screen itself rather than being told about a mounting
/// position. Everything below is deterministic geometry and statistics; there
/// is no trained model to be wrong about somebody else's laptop.
/// </para>
/// <para>
/// <b>Failure is a value, not an exception.</b> Every rejection path returns a
/// <see cref="ScreenDetection"/> with a reason and the numbers that led there,
/// because the caller's correct response (degrade to sensor, degrade to
/// manual) depends on WHY detection failed, not just that it did.
/// </para>
/// </summary>
public static class ScreenDetector
{
    /// <summary>
    /// Below this the frame has no usable dynamic range at all - a capped lens,
    /// a dark room, or a sensor that never actually delivered an image.
    ///
    /// Checked before Otsu on purpose. Otsu will happily split the noise floor
    /// of a near-uniform frame into "two modes" and hand back a threshold,
    /// which then turns sensor noise into a confident-looking region. Measured
    /// on the development laptop with the lens capped: 24 of 30 frames were
    /// called usable screens at up to 0.57 confidence before this guard went
    /// in. Separability alone did not catch it; the absolute spread does.
    /// </summary>
    public const double FlatFrameStd = 2.0;

    public static ScreenDetection Detect(GrayImage image, ScreenDetectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= ScreenDetectionOptions.Default;

        if (image.Width < 16 || image.Height < 16)
        {
            return Fail(ScreenDetectionReason.FrameTooSmall, options);
        }

        GrayImage work = image.Downscaled(options.MaxWorkSide);
        (double mean, double std) = MeanAndStd(work.Pixels);
        _ = mean;

        if (std < FlatFrameStd)
        {
            return Fail(ScreenDetectionReason.FlatScene, options) with { SceneStd = std };
        }

        OtsuResult otsu = OtsuThreshold.Compute(work);

        if (!otsu.IsBimodal)
        {
            // One brightness mode: no "screen vs background" pair exists, no
            // matter what threshold comes out. This is the capped lens / dark
            // room case, and it is a fact about the scene, not a bug.
            return Fail(ScreenDetectionReason.FlatScene, options) with
            {
                Threshold = otsu.Threshold,
                Separability = otsu.Separability,
                SceneStd = std,
            };
        }

        ScreenDetection bright = DetectPolarity(
            work, otsu, std, options, ScreenPolarity.Bright, foregroundIsBright: true);
        ScreenDetection dark = DetectPolarity(
            work, otsu, std, options, ScreenPolarity.Dark, foregroundIsBright: false);

        ScreenDetection best = bright.Confidence >= dark.Confidence ? bright : dark;
        if (best.Quad is not null && best.Confidence < options.UsableConfidence)
        {
            return best with { Reason = ScreenDetectionReason.LowConfidence };
        }

        return best;
    }

    private static ScreenDetection Fail(ScreenDetectionReason reason, ScreenDetectionOptions options)
        => new() { Reason = reason, UsableConfidence = options.UsableConfidence };

    private static (double Mean, double Std) MeanAndStd(ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length == 0)
        {
            return (0.0, 0.0);
        }

        double sum = 0.0;
        double sumSq = 0.0;
        foreach (byte b in pixels)
        {
            sum += b;
            sumSq += b * b;
        }

        double mean = sum / pixels.Length;
        return (mean, Math.Sqrt(Math.Max(0.0, (sumSq / pixels.Length) - (mean * mean))));
    }

    private static ScreenDetection DetectPolarity(
        GrayImage work,
        OtsuResult otsu,
        double sceneStd,
        ScreenDetectionOptions options,
        ScreenPolarity polarity,
        bool foregroundIsBright)
    {
        int width = work.Width;
        int height = work.Height;
        int total = width * height;
        int threshold = otsu.Threshold;

        var mask = new bool[total];
        int foregroundCount = 0;
        ReadOnlySpan<byte> pixels = work.Pixels;
        for (int i = 0; i < total; i++)
        {
            bool isForeground = foregroundIsBright ? pixels[i] >= threshold : pixels[i] < threshold;
            mask[i] = isForeground;
            if (isForeground)
            {
                foregroundCount++;
            }
        }

        if (foregroundCount == 0)
        {
            return new ScreenDetection
            {
                Reason = ScreenDetectionReason.NoForeground,
                Polarity = polarity,
                Threshold = threshold,
                Separability = otsu.Separability,
                SceneStd = sceneStd,
                UsableConfidence = options.UsableConfidence,
            };
        }

        int minArea = Math.Max(16, (int)(total * options.MinAreaFraction));
        IReadOnlyList<Blob> blobs = ConnectedComponents.Label(mask, width, height);

        ScreenDetection? best = null;
        bool sawCandidate = false;

        foreach (Blob blob in blobs)
        {
            if (blob.Area < minArea)
            {
                continue;
            }

            var outline = new List<(int X, int Y)>(blob.Area);
            foreach (int index in blob.Pixels)
            {
                outline.Add((index % width, index / width));
            }

            IReadOnlyList<(int X, int Y)> hull = ConvexHull.Compute(outline);
            NormalizedQuad? quad = QuadFit.FromHull(hull, width, height);
            if (quad is null)
            {
                continue;
            }

            sawCandidate = true;
            double hullArea = PolygonArea(hull);
            double rectangularity = hullArea > 0.0 ? Math.Min(1.0, blob.Area / hullArea) : 0.0;
            double quadAreaPixels = quad.Area * total;
            double fill = quadAreaPixels > 0.0 ? Math.Min(1.0, blob.Area / quadAreaPixels) : 0.0;
            double edge = EdgeContrast(work, quad);

            // Physical aspect, not normalized aspect: the aspect veto has to
            // mean the same thing on a 16:9 and a 4:3 camera (see QuadFit).
            double aspect = quad.AspectRatioFor((double)width / height);
            double confidence = Score(quad.Area, rectangularity, fill, edge, aspect);

            var candidate = new ScreenDetection
            {
                Reason = ScreenDetectionReason.Ok,
                Quad = quad,
                Confidence = confidence,
                Polarity = polarity,
                AreaFraction = quad.Area,
                Rectangularity = rectangularity,
                Fill = fill,
                EdgeContrast = edge,
                AspectRatio = aspect,
                TouchesFrameEdge = TouchesEdge(quad),
                Threshold = threshold,
                Separability = otsu.Separability,
                SceneStd = sceneStd,
                UsableConfidence = options.UsableConfidence,
            };

            if (best is null || candidate.Confidence > best.Confidence)
            {
                best = candidate;
            }
        }

        return best ?? new ScreenDetection
        {
            Reason = sawCandidate ? ScreenDetectionReason.DegenerateQuad : ScreenDetectionReason.NoForeground,
            Polarity = polarity,
            Threshold = threshold,
            Separability = otsu.Separability,
            SceneStd = sceneStd,
            UsableConfidence = options.UsableConfidence,
        };
    }

    /// <summary>
    /// Weighted confidence. The weights are a judgement call; the SHAPE of each
    /// term is not, and that is what the tests pin down: every term is a
    /// window that is 1.0 in the physically plausible range and falls to 0
    /// outside it, so no single freak measurement can carry a false positive.
    ///
    /// <para>
    /// <b>Three of the five are vetoes, not terms.</b> Aspect, edge contrast
    /// and whole-frame coverage each describe a way a candidate can be a
    /// perfectly-shaped rectangle that is definitely not a screen:
    /// </para>
    /// <list type="bullet">
    /// <item>a long bright line - perfect rectangle, aspect 18:1;</item>
    /// <item>sensor noise - perfect rectangle, boundary contrast ~8 levels;</item>
    /// <item>the background itself - perfect rectangle, fills the frame.</item>
    /// </list>
    /// <para>
    /// As additive terms with small weights all three still reached 0.55-0.7
    /// and were reported as usable screens. Measured, not theorised: the
    /// thin-line test and the capped-lens run each produced one. A veto has to
    /// be able to zero the score, so they multiply.
    /// </para>
    /// <para>
    /// The aspect taper at 2.8..4.0 also encodes a real limitation: as the lid
    /// closes the screen becomes a thin band, the aspect blows up, and
    /// detection is supposed to decline here rather than guess. The documented
    /// answer for that end of the motion is Prediction Mode
    /// (<c>HARDWARE_COMPATIBILITY</c> §2.8), not a low-confidence quad.
    /// </para>
    /// </summary>
    private static double Score(
        double areaFraction, double rectangularity, double fill, double edgeContrast, double aspect)
    {
        double area = Window(areaFraction, 0.02, 0.12, 0.60, 0.95);
        double rect = Window(rectangularity, 0.60, 0.85, 1.00, 1.00);
        double filled = Window(fill, 0.60, 0.85, 1.00, 1.00);

        // Contrast veto: a boundary below ~6 gray levels is not a boundary.
        // Above 22 it is unambiguous, and more contrast stops mattering.
        double contrastVeto = Window(edgeContrast, 6.0, 22.0, 1000.0, 1000.0);
        double shapeVeto = Window(aspect, 0.50, 0.90, 2.80, 4.00);
        double coverageVeto = HighTaper(areaFraction, 0.85, 0.95);

        double baseScore = (0.34 * area) + (0.36 * rect) + (0.30 * filled);
        return Math.Clamp(baseScore * contrastVeto * shapeVeto * coverageVeto, 0.0, 1.0);
    }

    /// <summary>
    /// 1.0 below <paramref name="plateauEnd"/>, 0.0 above <paramref name="zeroAt"/>.
    ///
    /// Used as a veto on "the candidate is basically the whole frame". Such a
    /// region has no measurable boundary - its edge contrast is whatever noise
    /// sits between one frame border and another - yet it scores a perfect
    /// rectangularity and fill because it IS a rectangle. Without this, the
    /// inverted polarity of a thin-line scene reports the background as a
    /// 98%-of-frame "screen" with 0.56 confidence. Measured, not hypothesised:
    /// that is exactly the failure the thin-line test caught.
    /// </summary>
    private static double HighTaper(double value, double plateauEnd, double zeroAt)
    {
        if (double.IsNaN(value) || value >= zeroAt)
        {
            return 0.0;
        }

        if (value <= plateauEnd)
        {
            return 1.0;
        }

        return (zeroAt - value) / (zeroAt - plateauEnd);
    }

    /// <summary>Trapezoid: 0 below <paramref name="zero"/>, 1 inside the plateau, 0 above <paramref name="zeroHigh"/>.</summary>
    private static double Window(double value, double zero, double one, double oneHigh, double zeroHigh)
    {
        if (double.IsNaN(value))
        {
            return 0.0;
        }

        if (value < zero || value > zeroHigh)
        {
            return 0.0;
        }

        if (value < one)
        {
            return (value - zero) / (one - zero);
        }

        if (value <= oneHigh)
        {
            return 1.0;
        }

        return (zeroHigh - value) / (zeroHigh - oneHigh);
    }

    /// <summary>
    /// |mean inside the quad - mean in a band just outside it|.
    ///
    /// The band is the quad scaled up about its centroid. Using the bounding
    /// box instead would measure the screen against its own bezel on two sides
    /// and against the room on the other two, which is not the contrast that
    /// distinguishes a display from what is behind it.
    /// </summary>
    private static double EdgeContrast(GrayImage work, NormalizedQuad quad)
    {
        double cx = (quad.TopLeft.X + quad.TopRight.X + quad.BottomRight.X + quad.BottomLeft.X) * 0.25;
        double cy = (quad.TopLeft.Y + quad.TopRight.Y + quad.BottomRight.Y + quad.BottomLeft.Y) * 0.25;
        NormalizedQuad outer = ScaleAbout(quad, cx, cy, 1.18);

        int minX = (int)Math.Floor(Math.Min(outer.TopLeft.X, Math.Min(outer.TopRight.X, Math.Min(outer.BottomRight.X, outer.BottomLeft.X))) * work.Width);
        int maxX = (int)Math.Ceiling(Math.Max(outer.TopLeft.X, Math.Max(outer.TopRight.X, Math.Max(outer.BottomRight.X, outer.BottomLeft.X))) * work.Width);
        int minY = (int)Math.Floor(Math.Min(outer.TopLeft.Y, Math.Min(outer.TopRight.Y, Math.Min(outer.BottomRight.Y, outer.BottomLeft.Y))) * work.Height);
        int maxY = (int)Math.Ceiling(Math.Max(outer.TopLeft.Y, Math.Max(outer.TopRight.Y, Math.Max(outer.BottomRight.Y, outer.BottomLeft.Y))) * work.Height);

        minX = Math.Max(0, minX);
        minY = Math.Max(0, minY);
        maxX = Math.Min(work.Width - 1, maxX);
        maxY = Math.Min(work.Height - 1, maxY);

        double insideSum = 0.0;
        int insideCount = 0;
        double ringSum = 0.0;
        int ringCount = 0;

        for (int y = minY; y <= maxY; y++)
        {
            double ny = (y + 0.5) / work.Height;
            for (int x = minX; x <= maxX; x++)
            {
                double nx = (x + 0.5) / work.Width;
                bool inInner = quad.Contains(nx, ny);
                bool inOuter = outer.Contains(nx, ny);
                if (inInner)
                {
                    insideSum += work[x, y];
                    insideCount++;
                }
                else if (inOuter)
                {
                    ringSum += work[x, y];
                    ringCount++;
                }
            }
        }

        if (insideCount == 0 || ringCount == 0)
        {
            return 0.0;
        }

        return Math.Abs((insideSum / insideCount) - (ringSum / ringCount));
    }

    private static NormalizedQuad ScaleAbout(NormalizedQuad quad, double cx, double cy, double factor)
    {
        QuadPoint Map(QuadPoint p) => new(
            cx + ((p.X - cx) * factor),
            cy + ((p.Y - cy) * factor));

        return new NormalizedQuad(
            Map(quad.TopLeft), Map(quad.TopRight), Map(quad.BottomRight), Map(quad.BottomLeft));
    }

    /// <summary>Any corner within one percent of the border counts as clipped.</summary>
    private static bool TouchesEdge(NormalizedQuad quad)
    {
        const double margin = 0.01;
        for (int i = 0; i < 4; i++)
        {
            QuadPoint p = quad[i];
            if (p.X <= margin || p.X >= 1.0 - margin || p.Y <= margin || p.Y >= 1.0 - margin)
            {
                return true;
            }
        }

        return false;
    }

    private static double PolygonArea(IReadOnlyList<(int X, int Y)> points)
    {
        if (points.Count < 3)
        {
            return 0.0;
        }

        double sum = 0.0;
        for (int i = 0; i < points.Count; i++)
        {
            (int X, int Y) a = points[i];
            (int X, int Y) b = points[(i + 1) % points.Count];
            sum += ((double)a.X * b.Y) - ((double)b.X * a.Y);
        }

        return Math.Abs(sum) * 0.5;
    }
}
