using System;
using System.Collections.Generic;
using DuoFlow.Vision;

namespace DuoFlow.Core.Tests;

/// <summary>
/// A scene described entirely in NORMALIZED coordinates, so the same scene can
/// be rendered at any resolution.
///
/// <para>
/// That is the whole point: the scale-invariance test renders one spec at
/// three sizes and requires one answer. If the spec carried pixels, the test
/// would be comparing three different scenes and would prove nothing.
/// </para>
/// </summary>
internal sealed record SceneSpec
{
    public byte Background { get; init; } = 35;

    public byte Screen { get; init; } = 210;

    /// <summary>Four corners in normalized space, TL / TR / BR / BL.</summary>
    public required QuadPoint[] Corners { get; init; }

    /// <summary>Extra brightness added toward the right edge (0 = flat background).</summary>
    public double BackgroundGradient { get; init; }

    /// <summary>Uniform noise amplitude in gray levels, deterministic per seed.</summary>
    public int Noise { get; init; }

    public int Seed { get; init; } = 12345;

    /// <summary>Additional quads painted on top of the screen, in draw order.</summary>
    public IReadOnlyList<(QuadPoint[] Corners, byte Value)>? Extras { get; init; }
}

internal static class SyntheticScene
{
    public static GrayImage Render(int width, int height, SceneSpec spec)
    {
        var pixels = new byte[width * height];
        var noise = new Lcg(spec.Seed);

        for (int y = 0; y < height; y++)
        {
            double ny = (y + 0.5) / height;
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                double nx = (x + 0.5) / width;
                double value = spec.Background + (spec.BackgroundGradient * nx);
                if (spec.Noise > 0)
                {
                    value += noise.Next(spec.Noise);
                }

                pixels[row + x] = Clamp(value);
            }
        }

        Paint(pixels, width, height, spec.Corners, spec.Screen);

        if (spec.Extras is not null)
        {
            foreach ((QuadPoint[] corners, byte value) in spec.Extras)
            {
                Paint(pixels, width, height, corners, value);
            }
        }

        return new GrayImage(width, height, pixels);
    }

    private static void Paint(byte[] pixels, int width, int height, QuadPoint[] corners, byte value)
    {
        var quad = new NormalizedQuad(corners[0], corners[1], corners[2], corners[3]);

        // Bounding box in pixels, so the fill is O(area) rather than O(frame).
        double minX = Math.Min(Math.Min(corners[0].X, corners[1].X), Math.Min(corners[2].X, corners[3].X));
        double maxX = Math.Max(Math.Max(corners[0].X, corners[1].X), Math.Max(corners[2].X, corners[3].X));
        double minY = Math.Min(Math.Min(corners[0].Y, corners[1].Y), Math.Min(corners[2].Y, corners[3].Y));
        double maxY = Math.Max(Math.Max(corners[0].Y, corners[1].Y), Math.Max(corners[2].Y, corners[3].Y));

        int x0 = Math.Max(0, (int)Math.Floor(minX * width));
        int x1 = Math.Min(width - 1, (int)Math.Ceiling(maxX * width));
        int y0 = Math.Max(0, (int)Math.Floor(minY * height));
        int y1 = Math.Min(height - 1, (int)Math.Ceiling(maxY * height));

        for (int y = y0; y <= y1; y++)
        {
            double ny = (y + 0.5) / height;
            for (int x = x0; x <= x1; x++)
            {
                double nx = (x + 0.5) / width;
                if (quad.Contains(nx, ny))
                {
                    pixels[(y * width) + x] = value;
                }
            }
        }
    }

    private static byte Clamp(double value) => (byte)Math.Clamp(value, 0.0, 255.0);

    /// <summary>Linear congruential generator: same seed, same noise, every run.</summary>
    private sealed class Lcg
    {
        private uint _state;

        public Lcg(int seed)
        {
            _state = seed == 0 ? 1u : (uint)seed;
        }

        public int Next(int amplitude)
        {
            _state = (_state * 1664525u) + 1013904223u;
            int span = (2 * amplitude) + 1;
            return (int)((_state >> 8) % (uint)span) - amplitude;
        }
    }
}

internal static class QuadAssert
{
    /// <summary>Largest per-corner distance between two quads, in normalized units.</summary>
    public static double MaxCornerError(NormalizedQuad a, NormalizedQuad b)
    {
        double worst = 0.0;
        for (int i = 0; i < 4; i++)
        {
            QuadPoint pa = a[i];
            QuadPoint pb = b[i];
            double dx = pa.X - pb.X;
            double dy = pa.Y - pb.Y;
            worst = Math.Max(worst, Math.Sqrt((dx * dx) + (dy * dy)));
        }

        return worst;
    }

    public static NormalizedQuad FromCorners(QuadPoint[] corners) =>
        new(corners[0], corners[1], corners[2], corners[3]);
}
