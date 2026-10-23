using System;
using System.Collections.Generic;
using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M3.2: turning an outline into four corners. The reason this is a general
/// quad and not a bounding box is the perspective case - a lid at 45 degrees
/// shows the screen as a trapezoid, and flattening it would destroy the very
/// signal M3.3 needs. So the trapezoid test is the one that matters most.
/// </summary>
public class QuadFitTests
{
    private const int Size = 100;

    private static List<(int X, int Y)> Rect(int x0, int y0, int x1, int y1)
        => new() { (x0, y0), (x1, y0), (x1, y1), (x0, y1) };

    [Fact]
    public void Rectangle_NormalizesAndOrdersCorners()
    {
        NormalizedQuad? quad = QuadFit.FromHull(Rect(10, 10, 90, 60), Size, Size);

        Assert.NotNull(quad);
        Assert.Equal(0.10, quad!.TopLeft.X, 3);
        Assert.Equal(0.10, quad.TopLeft.Y, 3);
        Assert.Equal(0.90, quad.TopRight.X, 3);
        Assert.Equal(0.10, quad.TopRight.Y, 3);
        Assert.Equal(0.90, quad.BottomRight.X, 3);
        Assert.Equal(0.60, quad.BottomRight.Y, 3);
        Assert.Equal(0.10, quad.BottomLeft.X, 3);
        Assert.Equal(0.60, quad.BottomLeft.Y, 3);
    }

    [Fact]
    public void Rectangle_AreaAndAspectAreExact()
    {
        NormalizedQuad quad = QuadFit.FromHull(Rect(10, 10, 90, 60), Size, Size)!;

        Assert.Equal(0.80 * 0.50, quad.Area, 6);
        Assert.Equal(1.60, quad.AspectRatioFor(1.0), 6);
    }

    [Fact]
    public void CornerOrder_IsClockwiseOnScreen()
    {
        // y grows downward, so a positive signed area means TL->TR->BR->BL,
        // i.e. clockwise as the user sees it.
        NormalizedQuad quad = QuadFit.FromHull(Rect(10, 10, 90, 60), Size, Size)!;
        Assert.True(quad.SignedArea > 0.0);
    }

    [Fact]
    public void Trapezoid_KeepsFourDistinctCorners()
    {
        // Perspective foreshortening: narrow top, wide bottom.
        var hull = new List<(int X, int Y)> { (30, 10), (70, 10), (90, 60), (10, 60) };

        NormalizedQuad quad = QuadFit.FromHull(hull, Size, Size)!;

        Assert.Equal(0.30, quad.TopLeft.X, 3);
        Assert.Equal(0.70, quad.TopRight.X, 3);
        Assert.Equal(0.90, quad.BottomRight.X, 3);
        Assert.Equal(0.10, quad.BottomLeft.X, 3);
        // A bounding box would report 0.80 / 0.50 = 1.6; the trapezoid is
        // squatter, and that difference IS the angle signal.
        Assert.True(quad.AspectRatioFor(1.0) < 1.5,
            $"aspect {quad.AspectRatioFor(1.0)} should show the foreshortening");
    }

    [Fact]
    public void RotatedSquare_AnchorsAtTheTopLeftMostCorner()
    {
        // Diamond: the topmost vertex is the natural anchor.
        var hull = new List<(int X, int Y)> { (50, 10), (90, 50), (50, 90), (10, 50) };

        NormalizedQuad quad = QuadFit.FromHull(hull, Size, Size)!;

        Assert.Equal(0.50, quad.TopLeft.X, 3);
        Assert.Equal(0.10, quad.TopLeft.Y, 3);
        Assert.True(quad.Area > 0.30);
    }

    [Fact]
    public void AspectRatioFor_CompensatesForTheFrameAspect()
    {
        // A physically SQUARE 300x300 region in a 16:9 frame. Raw normalized
        // distances call it 0.5625 - which sits on the aspect veto's 0.5 edge -
        // purely because the frame is wider than it is tall.
        var hull = new List<(int X, int Y)> { (300, 300), (600, 300), (600, 600), (300, 600) };
        NormalizedQuad quad = QuadFit.FromHull(hull, 1600, 900)!;

        Assert.Equal(0.5625, quad.AspectRatioFor(1.0), 3);
        Assert.Equal(1.0, quad.AspectRatioFor(16.0 / 9.0), 3);
    }

    [Fact]
    public void AspectRatioFor_RejectsNonPositiveImageAspect()
    {
        NormalizedQuad quad = QuadFit.FromHull(Rect(10, 10, 90, 60), Size, Size)!;
        Assert.Throws<ArgumentOutOfRangeException>(() => quad.AspectRatioFor(0.0));
    }

    [Fact]
    public void ManyVertices_ReduceToFour()
    {
        var hull = new List<(int X, int Y)>();
        for (int i = 0; i < 24; i++)
        {
            double angle = i * 2.0 * Math.PI / 24;
            hull.Add((
                (int)Math.Round(50 + (40 * Math.Cos(angle))),
                (int)Math.Round(50 + (40 * Math.Sin(angle)))));
        }

        NormalizedQuad? quad = QuadFit.FromHull(hull, Size, Size);

        Assert.NotNull(quad);
        // A quad inscribed in a circle of radius 0.4 has area 2r^2 = 0.32, so
        // the reduction must land near that - not collapse to a sliver, and
        // not somehow keep the circle's 0.50.
        Assert.InRange(quad!.Area, 0.25, 0.45);
    }

    [Fact]
    public void NormalizedOutput_StaysInsideTheUnitSquare()
    {
        NormalizedQuad quad = QuadFit.FromHull(Rect(0, 0, 100, 100), Size, Size)!;

        for (int i = 0; i < 4; i++)
        {
            Assert.InRange(quad[i].X, 0.0, 1.0);
            Assert.InRange(quad[i].Y, 0.0, 1.0);
        }
    }

    [Fact]
    public void Triangle_ReturnsNull()
    {
        Assert.Null(QuadFit.FromHull(new List<(int X, int Y)> { (0, 0), (10, 0), (0, 10) }, Size, Size));
    }

    [Fact]
    public void CollinearOutline_ReturnsNull()
    {
        Assert.Null(QuadFit.FromHull(
            new List<(int X, int Y)> { (0, 0), (10, 0), (20, 0), (30, 0) }, Size, Size));
    }

    [Fact]
    public void NonPositiveImageSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => QuadFit.FromHull(Rect(0, 0, 10, 10), 0, 10));
    }

    [Fact]
    public void Contains_IsTrueInsideAndFalseOutside()
    {
        NormalizedQuad quad = QuadFit.FromHull(Rect(10, 10, 90, 60), Size, Size)!;

        Assert.True(quad.Contains(0.5, 0.3));
        Assert.False(quad.Contains(0.05, 0.3));
        Assert.False(quad.Contains(0.5, 0.95));
    }

    [Fact]
    public void Contains_HandlesTheTrapezoid()
    {
        var hull = new List<(int X, int Y)> { (30, 10), (70, 10), (90, 60), (10, 60) };
        NormalizedQuad quad = QuadFit.FromHull(hull, Size, Size)!;

        Assert.True(quad.Contains(0.5, 0.5));     // middle
        Assert.False(quad.Contains(0.15, 0.15));  // above the narrow top-left
        Assert.True(quad.Contains(0.15, 0.55));   // inside the wide bottom
    }

    [Fact]
    public void Flatten_MatchesCornerOrder()
    {
        NormalizedQuad quad = QuadFit.FromHull(Rect(10, 10, 90, 60), Size, Size)!;
        double[] flat = quad.Flatten();

        Assert.Equal(8, flat.Length);
        Assert.Equal(quad.TopLeft.X, flat[0], 6);
        Assert.Equal(quad.TopLeft.Y, flat[1], 6);
        Assert.Equal(quad.BottomLeft.Y, flat[7], 6);
    }
}
