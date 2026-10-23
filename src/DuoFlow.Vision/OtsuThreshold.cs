using System;

namespace DuoFlow.Vision;

/// <summary>
/// Otsu's method: the threshold that maximizes between-class variance.
///
/// <para>
/// Chosen over a fixed threshold (which cannot survive a room getting darker)
/// and over an adaptive local threshold (which needs a block size in pixels -
/// i.e. exactly the resolution dependency this project is trying to avoid).
/// Otsu reads the histogram of the frame it was given, so it adapts to the
/// lighting by construction and stays resolution-independent.
/// </para>
/// <para>
/// <see cref="Separability"/> is reported alongside because it is free and it
/// is the honest answer to "was there even anything to split?". A flat frame
/// and a two-mode frame both produce a threshold; only the second one means
/// anything. Callers use it to explain a low confidence instead of guessing.
/// </para>
/// </summary>
public readonly record struct OtsuResult(int Threshold, double Separability)
{
    /// <summary>
    /// Below this the histogram is effectively unimodal - there is no "screen"
    /// and "background" pair to find, whatever threshold comes out.
    /// </summary>
    public const double FlatSceneSeparability = 0.35;

    public bool IsBimodal => Separability >= FlatSceneSeparability;
}

public static class OtsuThreshold
{
    /// <summary>
    /// Returns the threshold such that pixels <c>&gt;= Threshold</c> are
    /// foreground. Never throws on a degenerate frame: an all-one-value image
    /// yields that value with separability 0, which the caller can explain.
    /// </summary>
    public static OtsuResult Compute(GrayImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Compute(image.Pixels);
    }

    public static OtsuResult Compute(ReadOnlySpan<byte> gray)
    {
        if (gray.Length == 0)
        {
            return new OtsuResult(0, 0.0);
        }

        Span<int> histogram = stackalloc int[256];
        histogram.Clear();
        foreach (byte b in gray)
        {
            histogram[b]++;
        }

        int total = gray.Length;
        double sumAll = 0.0;
        for (int i = 0; i < 256; i++)
        {
            sumAll += i * (double)histogram[i];
        }

        double sumBackground = 0.0;
        int weightBackground = 0;
        double bestVariance = -1.0;
        int bestThreshold = 0;

        for (int t = 0; t < 256; t++)
        {
            weightBackground += histogram[t];
            if (weightBackground == 0)
            {
                continue;
            }

            int weightForeground = total - weightBackground;
            if (weightForeground == 0)
            {
                break;
            }

            sumBackground += t * (double)histogram[t];

            double meanBackground = sumBackground / weightBackground;
            double meanForeground = (sumAll - sumBackground) / weightForeground;
            double delta = meanBackground - meanForeground;
            double variance = (double)weightBackground * weightForeground * delta * delta;

            if (variance > bestVariance)
            {
                bestVariance = variance;
                bestThreshold = t;
            }
        }

        // Normalize: variance scales with (pixel count)^2, so raw values are not
        // comparable between resolutions. Dividing by total^2 gives the
        // between-class share of total variance, which is 0..1 and comparable -
        // and the scale-invariance test would fail on the raw number.
        double separability = 0.0;
        double totalVariance = 0.0;
        double mean = sumAll / total;
        for (int i = 0; i < 256; i++)
        {
            if (histogram[i] == 0)
            {
                continue;
            }

            double d = i - mean;
            totalVariance += histogram[i] * d * d;
        }

        if (totalVariance > 0.0)
        {
            separability = Math.Clamp(bestVariance / totalVariance, 0.0, 1.0);
        }

        // The scan yields the last index of the background class; foreground
        // starts one above it. Returning t+1 means "pixels >= threshold are
        // foreground" holds for a clean two-level image (e.g. 0 and 255).
        return new OtsuResult(Math.Min(bestThreshold + 1, 255), separability);
    }
}
