using System;
using System.Collections.Generic;

namespace DuoFlow.Vision;

/// <summary>
/// Convex hull via Andrew's monotone chain.
///
/// <para>
/// Needed because the quad fit must not be pulled inward by ragged edges: a
/// thresholded screen outline has stray pixels along every boundary (a
/// reflection, a half-lit bezel), and fitting to the raw outline would cut the
/// corners. The hull is the outermost boundary, so the fit only ever has to
/// deal with the true silhouette.
/// </para>
/// <para>
/// O(n log n) from the sort, which at a 160 px working size is irrelevant next
/// to the threshold pass that produced the points.
/// </para>
/// </summary>
public static class ConvexHull
{
    /// <summary>
    /// Returns the hull vertices in counter-clockwise order (mathematical
    /// convention, i.e. y is treated as up). Collinear interior points are
    /// dropped. Returns fewer than 3 points when the input is degenerate.
    /// </summary>
    public static IReadOnlyList<(int X, int Y)> Compute(IReadOnlyList<(int X, int Y)> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 3)
        {
            return points;
        }

        var sorted = new List<(int X, int Y)>(points);
        sorted.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));

        // Duplicates make the turn test divide by zero-length edges; drop them.
        var unique = new List<(int X, int Y)>(sorted.Count);
        foreach ((int X, int Y) p in sorted)
        {
            if (unique.Count == 0 || unique[^1] != p)
            {
                unique.Add(p);
            }
        }

        if (unique.Count < 3)
        {
            return unique;
        }

        var hull = new List<(int X, int Y)>((2 * unique.Count) + 1);

        foreach ((int X, int Y) p in unique)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(p);
        }

        int lowerCount = hull.Count + 1;
        for (int i = unique.Count - 2; i >= 0; i--)
        {
            (int X, int Y) p = unique[i];
            while (hull.Count >= lowerCount && Cross(hull[^2], hull[^1], p) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(p);
        }

        // The chain closes on its own: the last point repeats the first.
        hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    /// <summary>Cross product of (b-a) x (c-a). Positive = left turn.</summary>
    private static long Cross((int X, int Y) a, (int X, int Y) b, (int X, int Y) c)
    {
        return ((long)(b.X - a.X) * (c.Y - a.Y)) - ((long)(b.Y - a.Y) * (c.X - a.X));
    }
}
