using System;
using System.Collections.Generic;

namespace DuoFlow.Vision;

/// <summary>One connected region of foreground pixels, in the image's own grid.</summary>
public sealed class Blob
{
    internal Blob(int label, List<int> pixels, int width)
    {
        Label = label;
        Pixels = pixels;

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        long sumX = 0, sumY = 0;
        foreach (int index in pixels)
        {
            int x = index % width;
            int y = index / width;
            sumX += x;
            sumY += y;
            if (x < minX) { minX = x; }
            if (x > maxX) { maxX = x; }
            if (y < minY) { minY = y; }
            if (y > maxY) { maxY = y; }
        }

        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
        CentroidX = pixels.Count > 0 ? (double)sumX / pixels.Count : 0.0;
        CentroidY = pixels.Count > 0 ? (double)sumY / pixels.Count : 0.0;
    }

    public int Label { get; }

    public int Area => Pixels.Count;

    public int MinX { get; }

    public int MinY { get; }

    public int MaxX { get; }

    public int MaxY { get; }

    public int Width => MaxX - MinX + 1;

    public int Height => MaxY - MinY + 1;

    public double CentroidX { get; }

    public double CentroidY { get; }

    /// <summary>Pixel indices (y * imageWidth + x) belonging to this region.</summary>
    public IReadOnlyList<int> Pixels { get; }

    public (int X, int Y) ToPoint(int index, int imageWidth) => (index % imageWidth, index / imageWidth);
}

/// <summary>
/// Two-pass connected-component labelling, 4-connectivity.
///
/// <para>
/// 4-connectivity rather than 8: with 8, two bright regions that merely touch
/// at a corner merge, and a screen edge that grazes a bright reflection would
/// drag the reflection into the same blob - which then wrecks the quad fit.
/// Requiring a shared edge is the conservative choice, and the failure mode of
/// being too strict (one screen split into two) is caught downstream by the
/// area threshold instead of corrupting a quad.
/// </para>
/// <para>
/// Union-find with path compression on a single flat array. The input is a
/// downscaled frame (at most ~160x160), so the O(n) allocations here are
/// noise next to the 8 MB camera buffer that produced it.
/// </para>
/// </summary>
public static class ConnectedComponents
{
    public static IReadOnlyList<Blob> Label(ReadOnlySpan<bool> mask, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "dimensions must be positive");
        }

        if (mask.Length != width * height)
        {
            throw new ArgumentException(
                $"mask length {mask.Length} does not match {width}x{height}", nameof(mask));
        }

        int count = mask.Length;
        var labels = new int[count];
        var parent = new int[count + 1];   // label 0 = background
        for (int i = 0; i <= count; i++)
        {
            parent[i] = i;
        }

        int next = 1;
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = row + x;
                if (!mask[i])
                {
                    continue;
                }

                int left = x > 0 ? labels[i - 1] : 0;
                int up = y > 0 ? labels[i - width] : 0;

                if (left == 0 && up == 0)
                {
                    labels[i] = next++;
                }
                else if (left != 0 && up != 0)
                {
                    labels[i] = left;
                    if (left != up)
                    {
                        Union(parent, left, up);
                    }
                }
                else
                {
                    labels[i] = left != 0 ? left : up;
                }
            }
        }

        // Second pass: resolve roots, bucket pixels by final label.
        var buckets = new Dictionary<int, List<int>>();
        for (int i = 0; i < count; i++)
        {
            if (labels[i] == 0)
            {
                continue;
            }

            int root = Find(parent, labels[i]);
            if (!buckets.TryGetValue(root, out List<int>? list))
            {
                list = new List<int>();
                buckets[root] = list;
            }

            list.Add(i);
        }

        var blobs = new List<Blob>(buckets.Count);
        foreach (KeyValuePair<int, List<int>> pair in buckets)
        {
            blobs.Add(new Blob(pair.Key, pair.Value, width));
        }

        // Largest first: the screen is expected to be the biggest thing that
        // survives the threshold, and a stable order makes the detector's
        // tie-breaking reproducible.
        blobs.Sort((a, b) => b.Area.CompareTo(a.Area));
        return blobs;
    }

    private static int Find(int[] parent, int i)
    {
        int root = i;
        while (parent[root] != root)
        {
            root = parent[root];
        }

        while (parent[i] != root)
        {
            int next = parent[i];
            parent[i] = root;
            i = next;
        }

        return root;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int ra = Find(parent, a);
        int rb = Find(parent, b);
        if (ra != rb)
        {
            parent[rb] = ra;
        }
    }
}
