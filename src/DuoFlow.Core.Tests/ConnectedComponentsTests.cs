using System;
using System.Collections.Generic;
using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M3.2: grouping foreground pixels into candidate regions.
/// The 4- vs 8-connectivity choice is the one that matters: with 8, a screen
/// edge grazing a bright reflection merges into one blob and wrecks the quad.
/// </summary>
public class ConnectedComponentsTests
{
    private static bool[] Mask(string[] rows)
    {
        int height = rows.Length;
        int width = rows[0].Length;
        var mask = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            Assert.Equal(width, rows[y].Length);
            for (int x = 0; x < width; x++)
            {
                mask[(y * width) + x] = rows[y][x] == '#';
            }
        }

        return mask;
    }

    [Fact]
    public void TwoSeparateBlobs_AreTwoComponents()
    {
        IReadOnlyList<Blob> blobs = ConnectedComponents.Label(
            Mask(new[]
            {
                "##...",
                "##...",
                ".....",
                "..###",
                "..###",
            }),
            5,
            5);

        Assert.Equal(2, blobs.Count);
        // Largest first: the 3x2 block, then the 2x2 one.
        Assert.Equal(6, blobs[0].Area);
        Assert.Equal(4, blobs[1].Area);
    }

    [Fact]
    public void DiagonalTouch_DoesNotConnect()
    {
        // 4-connectivity: sharing only a corner is not sharing an edge.
        IReadOnlyList<Blob> blobs = ConnectedComponents.Label(
            Mask(new[]
            {
                "#.",
                ".#",
            }),
            2,
            2);

        Assert.Equal(2, blobs.Count);
    }

    [Fact]
    public void EdgeTouch_Connects()
    {
        IReadOnlyList<Blob> blobs = ConnectedComponents.Label(
            Mask(new[]
            {
                "##",
                ".#",
            }),
            2,
            2);

        Assert.Single(blobs);
        Assert.Equal(3, blobs[0].Area);
    }

    [Fact]
    public void SortedByAreaDescending()
    {
        IReadOnlyList<Blob> blobs = ConnectedComponents.Label(
            Mask(new[]
            {
                "#....####",
                "#....####",
                "#....####",
                "#........",
            }),
            9,
            4);

        Assert.Equal(2, blobs.Count);
        Assert.True(blobs[0].Area > blobs[1].Area);
        Assert.Equal(12, blobs[0].Area);
        Assert.Equal(4, blobs[1].Area);
    }

    [Fact]
    public void ReportsBoundsAndCentroid()
    {
        IReadOnlyList<Blob> blobs = ConnectedComponents.Label(
            Mask(new[]
            {
                ".....",
                ".###.",
                ".###.",
                ".....",
            }),
            5,
            4);

        Blob blob = Assert.Single(blobs);
        Assert.Equal(1, blob.MinX);
        Assert.Equal(1, blob.MinY);
        Assert.Equal(3, blob.MaxX);
        Assert.Equal(2, blob.MaxY);
        Assert.Equal(3, blob.Width);
        Assert.Equal(2, blob.Height);
        Assert.Equal(2.0, blob.CentroidX, 6);
        Assert.Equal(1.5, blob.CentroidY, 6);
    }

    [Fact]
    public void EmptyMask_YieldsNothing()
    {
        Assert.Empty(ConnectedComponents.Label(new bool[25], 5, 5));
    }

    [Fact]
    public void FullMask_IsOneBlob()
    {
        var mask = new bool[25];
        Array.Fill(mask, true);

        Blob blob = Assert.Single(ConnectedComponents.Label(mask, 5, 5));
        Assert.Equal(25, blob.Area);
    }

    [Fact]
    public void MismatchedMaskLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => ConnectedComponents.Label(new bool[10], 5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConnectedComponents.Label(new bool[10], 0, 5));
    }

    [Fact]
    public void MergesTwoRunsThatMeetOnALaterRow()
    {
        // Classic union-find case: the two arms of a U must end up one blob.
        IReadOnlyList<Blob> blobs = ConnectedComponents.Label(
            Mask(new[]
            {
                "#...#",
                "#...#",
                "#####",
            }),
            5,
            3);

        Blob blob = Assert.Single(blobs);
        Assert.Equal(9, blob.Area);   // 2 + 2 + 5
    }
}
