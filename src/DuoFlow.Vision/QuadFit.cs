using System;
using System.Collections.Generic;

namespace DuoFlow.Vision;

/// <summary>A point in normalized image space: 0..1 on both axes.</summary>
public readonly record struct QuadPoint(double X, double Y);

/// <summary>
/// The detected screen, as four corners in normalized image coordinates.
///
/// <para>
/// Corner order is fixed and documented because every consumer depends on it:
/// <see cref="TopLeft"/> is the corner nearest the top-left of the image, then
/// the remaining corners follow <b>clockwise on screen</b>
/// (TopLeft → TopRight → BottomRight → BottomLeft).
/// </para>
/// <para>
/// "TopLeft" is a label, not a claim about the physical laptop. A camera below
/// the screen, or a rolled laptop, moves which corner is physically the
/// screen's top-left - that is M3.5's calibration problem, not this one. What
/// M3.2 guarantees is a consistent, image-anchored winding so the next stage
/// never has to guess.
/// </para>
/// </summary>
public sealed record NormalizedQuad(
    QuadPoint TopLeft,
    QuadPoint TopRight,
    QuadPoint BottomRight,
    QuadPoint BottomLeft)
{
    public QuadPoint this[int index] => index switch
    {
        0 => TopLeft,
        1 => TopRight,
        2 => BottomRight,
        3 => BottomLeft,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    /// <summary>Absolute area in normalized units (whole image = 1.0).</summary>
    public double Area => Math.Abs(SignedArea);

    /// <summary>Positive = clockwise on screen (y grows downward).</summary>
    public double SignedArea
    {
        get
        {
            double sum = 0.0;
            for (int i = 0; i < 4; i++)
            {
                QuadPoint a = this[i];
                QuadPoint b = this[(i + 1) % 4];
                sum += (a.X * b.Y) - (b.X * a.Y);
            }

            return sum * 0.5;
        }
    }

    /// <summary>
    /// Mean horizontal side divided by mean vertical side, measured in the
    /// PIXELS of a frame whose own aspect is <paramref name="imageAspect"/>
    /// (width/height).
    ///
    /// <para>
    /// <b>The image aspect is a required argument, not an assumption.</b>
    /// Normalized coordinates squash a 16:9 frame into a unit square, so a
    /// square object occupying 0.3 x 0.3 of normalized space is really
    /// 0.3 x 0.533 in pixels. Comparing raw normalized distances therefore
    /// reports 0.56 for something that is physically square - and 0.56 sits
    /// right on the aspect veto, so a square object in a 16:9 frame would be
    /// rejected as "not a screen" for a reason that has nothing to do with
    /// the object. The same code on a 4:3 camera would behave differently
    /// again, which is exactly the resolution dependence this project exists
    /// to avoid.
    /// </para>
    /// </summary>
    public double AspectRatioFor(double imageAspect)
    {
        if (!(imageAspect > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(imageAspect), "image aspect must be positive");
        }

        double top = Distance(TopLeft, TopRight, imageAspect);
        double bottom = Distance(BottomLeft, BottomRight, imageAspect);
        double left = Distance(TopLeft, BottomLeft, imageAspect);
        double right = Distance(TopRight, BottomRight, imageAspect);
        double h = (left + right) * 0.5;
        return h > 1e-9 ? (top + bottom) * 0.5 / h : 0.0;
    }

    /// <summary>True when the point lies inside the quad (edges inclusive).</summary>
    public bool Contains(double x, double y)
    {
        // Convex polygon: the point is inside when it is on the same side of
        // every edge. The expected sign is taken from the polygon's own
        // winding so this works whichever way the corners came out.
        double reference = 0.0;
        for (int i = 0; i < 4; i++)
        {
            QuadPoint a = this[i];
            QuadPoint b = this[(i + 1) % 4];
            double cross = ((b.X - a.X) * (y - a.Y)) - ((b.Y - a.Y) * (x - a.X));
            if (Math.Abs(cross) < 1e-12)
            {
                continue;
            }

            if (reference == 0.0)
            {
                reference = Math.Sign(cross);
            }
            else if (Math.Sign(cross) != Math.Sign(reference))
            {
                return false;
            }
        }

        return true;
    }

    public double[] Flatten() => new[]
    {
        TopLeft.X, TopLeft.Y,
        TopRight.X, TopRight.Y,
        BottomRight.X, BottomRight.Y,
        BottomLeft.X, BottomLeft.Y,
    };

    private static double Distance(QuadPoint a, QuadPoint b, double imageAspect = 1.0)
    {
        double dx = (b.X - a.X) * imageAspect;
        double dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

/// <summary>
/// Reduces a blob's outline to four corners.
///
/// <para>
/// <b>Why not a bounding box or a min-area rectangle.</b> The whole point of
/// M3.2 is to feed an angle estimate: a lid at 45 degrees shows the screen as
/// a TRAPEZOID, and both of those fits would flatten it back into a rectangle
/// and destroy exactly the signal M3.3 needs. A general quadrilateral is the
/// minimum that can carry perspective.
/// </para>
/// <para>
/// <b>Why Visvalingam-Whyatt.</b> Repeatedly removing the vertex whose
/// triangle covers the least area is deterministic, has no parameters to tune,
/// and means something geometric ("this corner is nearly collinear, drop it").
/// For a near-rectangular blob it converges to the four real corners instead of
/// to whichever four happen to be extreme along fixed axes - which is what the
/// cheaper "extreme x+y / x-y" trick does, and it fails as soon as the laptop
/// is rolled a few degrees.
/// </para>
/// </summary>
public static class QuadFit
{
    public const int CornerCount = 4;

    /// <summary>
    /// Fits a quad to <paramref name="hull"/> (a convex outline in pixel
    /// coordinates) and normalizes by the image size.
    /// Returns null when the outline cannot support four corners.
    /// </summary>
    public static NormalizedQuad? FromHull(IReadOnlyList<(int X, int Y)> hull, int imageWidth, int imageHeight)
    {
        ArgumentNullException.ThrowIfNull(hull);
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageWidth), "image size must be positive");
        }

        if (hull.Count < CornerCount)
        {
            return null;
        }

        var points = new List<(double X, double Y)>(hull.Count);
        foreach ((int x, int y) in hull)
        {
            points.Add((x, y));
        }

        while (points.Count > CornerCount)
        {
            int victim = -1;
            double smallest = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                (double X, double Y) prev = points[(i - 1 + points.Count) % points.Count];
                (double X, double Y) current = points[i];
                (double X, double Y) next = points[(i + 1) % points.Count];

                double area = Math.Abs(
                    ((current.X - prev.X) * (next.Y - prev.Y)) -
                    ((next.X - prev.X) * (current.Y - prev.Y))) * 0.5;

                if (area < smallest)
                {
                    smallest = area;
                    victim = i;
                }
            }

            if (victim < 0)
            {
                return null;
            }

            points.RemoveAt(victim);
        }

        if (points.Count != CornerCount)
        {
            return null;
        }

        if (Math.Abs(PolygonArea(points)) < 1e-6)
        {
            return null;   // all four collapsed onto one line
        }

        // Anchor at the corner nearest the image's top-left, then wind
        // clockwise on screen. Sorting by angle first guarantees a consistent
        // rotational order regardless of how the hull came in.
        double cx = 0.0, cy = 0.0;
        foreach ((double X, double Y) p in points)
        {
            cx += p.X;
            cy += p.Y;
        }

        cx /= points.Count;
        cy /= points.Count;

        points.Sort((a, b) =>
        {
            double aa = Math.Atan2(a.Y - cy, a.X - cx);
            double ab = Math.Atan2(b.Y - cy, b.X - cx);
            return aa.CompareTo(ab);
        });

        // atan2 ascending runs clockwise on screen because y grows downward,
        // so starting at the top-left-most corner gives TL, TR, BR, BL.
        int anchor = 0;
        double best = double.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            double score = points[i].X + points[i].Y;
            if (score < best)
            {
                best = score;
                anchor = i;
            }
        }

        var ordered = new QuadPoint[CornerCount];
        for (int i = 0; i < CornerCount; i++)
        {
            (double X, double Y) p = points[(anchor + i) % CornerCount];
            ordered[i] = new QuadPoint(
                Math.Clamp(p.X / imageWidth, 0.0, 1.0),
                Math.Clamp(p.Y / imageHeight, 0.0, 1.0));
        }

        return new NormalizedQuad(ordered[0], ordered[1], ordered[2], ordered[3]);
    }

    private static double PolygonArea(List<(double X, double Y)> points)
    {
        double sum = 0.0;
        for (int i = 0; i < points.Count; i++)
        {
            (double X, double Y) a = points[i];
            (double X, double Y) b = points[(i + 1) % points.Count];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum * 0.5;
    }
}
