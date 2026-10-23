using System;

namespace DuoFlow.Vision;

/// <summary>
/// An 8-bit luminance image, and the ONLY pixel container in this project.
///
/// <para>
/// <b>Why this type exists at all.</b> DD-053 says nothing in
/// <c>DuoFlow.Vision</c> may see a pixel coordinate. A detection algorithm has
/// to index its input grid - that is a physical fact, not a style choice - so
/// the rule is enforced at the two EDGES instead of in the middle: pixels come
/// in through <see cref="FromBgra"/> and nothing else, and every result that
/// leaves this project is normalized. Between those edges the algorithms may
/// index freely, because there is no resolution to be wrong about.
/// </para>
/// <para>
/// <b>The property that actually keeps it honest is tested, not asserted:</b>
/// the same synthetic scene rendered at 480x270, 960x540 and 1280x720 must
/// produce the same normalized quad. A detector that secretly depends on the
/// pixel count fails that test immediately.
/// </para>
/// <para>
/// Gray rather than colour on purpose: a laptop display is distinguished from
/// its surroundings by brightness and edges, not by hue, and one channel is a
/// quarter of the work. Colour would buy nothing here and cost 4x the scan.
/// </para>
/// </summary>
public sealed class GrayImage
{
    private readonly byte[] _gray;

    public GrayImage(int width, int height, byte[] gray)
    {
        ArgumentNullException.ThrowIfNull(gray);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "image dimensions must be positive");
        }

        if (gray.Length != width * height)
        {
            throw new ArgumentException(
                $"gray length {gray.Length} does not match {width}x{height}", nameof(gray));
        }

        Width = width;
        Height = height;
        _gray = gray;
    }

    public int Width { get; }

    public int Height { get; }

    public int PixelCount => Width * Height;

    public ReadOnlySpan<byte> Pixels => _gray;

    public byte this[int x, int y] => _gray[(y * Width) + x];

    /// <summary>
    /// The single door pixels come through: BGRA8 in, luminance out.
    ///
    /// Rec.601 weights, matching what the acceptance probe already reports, so
    /// "mean luminance 15" in a probe log and "threshold 18" in a detection log
    /// are talking about the same number.
    /// </summary>
    public static GrayImage FromBgra(int width, int height, ReadOnlySpan<byte> bgra)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "image dimensions must be positive");
        }

        long needed = (long)width * height * 4;
        if (bgra.Length < needed)
        {
            throw new ArgumentException(
                $"bgra needs {needed} bytes for {width}x{height}, got {bgra.Length}", nameof(bgra));
        }

        var gray = new byte[width * height];
        for (int i = 0, o = 0; i < gray.Length; i++, o += 4)
        {
            // Integer arithmetic: 77/150/29 over 256 is the standard fixed-point
            // Rec.601 luma and avoids a float round-trip per pixel. The +128 is
            // rounding, not decoration: without it every pixel is biased low by
            // up to one level, which shows up as "pure green reads 149".
            gray[i] = (byte)(((bgra[o] * 29) + (bgra[o + 1] * 150) + (bgra[o + 2] * 77) + 128) >> 8);
        }

        return new GrayImage(width, height, gray);
    }

    /// <summary>
    /// Box-filtered downscale so the long side is at most <paramref name="maxSide"/>.
    ///
    /// <para>
    /// <b>Area average, not nearest neighbour.</b> Nearest neighbour picks one
    /// pixel per block, so a one-pixel shift changes which one - and the
    /// detected edge then moves by a whole output pixel. At 160 px that is a
    /// visible jump in normalized coordinates, which is exactly the
    /// resolution-dependent behaviour the scale-invariance test exists to
    /// catch. Averaging makes the result continuous in the input.
    /// </para>
    /// <para>
    /// Already small enough: returned as-is (same instance) rather than copied.
    /// </para>
    /// </summary>
    public GrayImage Downscaled(int maxSide)
    {
        if (maxSide <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSide), "maxSide must be positive");
        }

        int longest = Math.Max(Width, Height);
        if (longest <= maxSide)
        {
            return this;
        }

        double scale = (double)maxSide / longest;
        int newWidth = Math.Max(1, (int)Math.Round(Width * scale));
        int newHeight = Math.Max(1, (int)Math.Round(Height * scale));

        var dst = new byte[newWidth * newHeight];
        for (int y = 0; y < newHeight; y++)
        {
            // Integer block edges derived from the ratio, so blocks tile the
            // source exactly with no gaps and no overlap at any scale.
            int y0 = (int)((long)y * Height / newHeight);
            int y1 = (int)((long)(y + 1) * Height / newHeight);
            if (y1 <= y0)
            {
                y1 = y0 + 1;
            }

            for (int x = 0; x < newWidth; x++)
            {
                int x0 = (int)((long)x * Width / newWidth);
                int x1 = (int)((long)(x + 1) * Width / newWidth);
                if (x1 <= x0)
                {
                    x1 = x0 + 1;
                }

                int sum = 0;
                int count = 0;
                for (int sy = y0; sy < y1; sy++)
                {
                    int row = sy * Width;
                    for (int sx = x0; sx < x1; sx++)
                    {
                        sum += _gray[row + sx];
                        count++;
                    }
                }

                dst[(y * newWidth) + x] = (byte)(sum / count);
            }
        }

        return new GrayImage(newWidth, newHeight, dst);
    }
}
