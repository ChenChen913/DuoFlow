using System;
using System.Collections.Generic;
using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M3.2: the outer boundary a quad is fitted to. Its job is to stop ragged
/// threshold edges (a reflection, a half-lit bezel) from pulling the corners
/// inward, so the properties that matter are "stray interior points are
/// ignored" and "the result is actually convex".
/// </summary>
public class ConvexHullTests
{
    [Fact]
    public void SquareWithInteriorPoints_ReturnsFourCorners()
    {
        var points = new List<(int X, int Y)>();
        for (int y = 0; y <= 4; y++)
        {
            for (int x = 0; x <= 4; x++)
            {
                points.Add((x, y));
            }
        }

        IReadOnlyList<(int X, int Y)> hull = ConvexHull.Compute(points);

        Assert.Equal(4, hull.Count);
        Assert.Contains((0, 0), hull);
        Assert.Contains((4, 0), hull);
        Assert.Contains((4, 4), hull);
        Assert.Contains((0, 4), hull);
    }

    [Fact]
    public void CollinearPoints_AreNotHullVertices()
    {
        // Midpoints along an edge must be dropped, or the quad fit has extra
        // vertices to chew through and Visvalingam's tie-breaking gets noisy.
        var points = new List<(int X, int Y)>
        {
            (0, 0), (1, 0), (2, 0), (3, 0),
            (3, 3),
            (0, 3),
        };

        IReadOnlyList<(int X, int Y)> hull = ConvexHull.Compute(points);

        Assert.Equal(4, hull.Count);
        Assert.DoesNotContain((1, 0), hull);
        Assert.DoesNotContain((2, 0), hull);
    }

    [Fact]
    public void Triangle_ReturnsThree()
    {
        IReadOnlyList<(int X, int Y)> hull = ConvexHull.Compute(new List<(int X, int Y)>
        {
            (0, 0), (10, 0), (0, 10),
        });

        Assert.Equal(3, hull.Count);
    }

    [Fact]
    public void SingleLine_ReturnsItsEndpoints()
    {
        IReadOnlyList<(int X, int Y)> hull = ConvexHull.Compute(new List<(int X, int Y)>
        {
            (0, 0), (1, 1), (2, 2), (3, 3),
        });

        Assert.Equal(2, hull.Count);
    }

    [Fact]
    public void FewerThanThreePoints_AreReturnedAsIs()
    {
        Assert.Empty(ConvexHull.Compute(new List<(int X, int Y)>()));
        Assert.Single(ConvexHull.Compute(new List<(int X, int Y)> { (5, 5) }));
    }

    [Fact]
    public void DuplicatePoints_DoNotBreakTheHull()
    {
        IReadOnlyList<(int X, int Y)> hull = ConvexHull.Compute(new List<(int X, int Y)>
        {
            (0, 0), (0, 0), (4, 0), (4, 0), (4, 4), (0, 4),
        });

        Assert.Equal(4, hull.Count);
    }

    [Fact]
    public void HullOfNoise_IsConvex()
    {
        var rng = new Random(99);
        var points = new List<(int X, int Y)>();
        for (int i = 0; i < 200; i++)
        {
            points.Add((rng.Next(0, 100), rng.Next(0, 100)));
        }

        IReadOnlyList<(int X, int Y)> hull = ConvexHull.Compute(points);

        Assert.True(hull.Count >= 3);
        // Every turn must go the same way, or it is not a convex polygon.
        int sign = 0;
        for (int i = 0; i < hull.Count; i++)
        {
            (int X, int Y) a = hull[i];
            (int X, int Y) b = hull[(i + 1) % hull.Count];
            (int X, int Y) c = hull[(i + 2) % hull.Count];
            long cross = ((long)(b.X - a.X) * (c.Y - a.Y)) - ((long)(b.Y - a.Y) * (c.X - a.X));
            if (cross == 0)
            {
                continue;
            }

            int s = Math.Sign(cross);
            if (sign == 0)
            {
                sign = s;
            }
            else
            {
                Assert.Equal(sign, s);
            }
        }
    }
}
