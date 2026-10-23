using System;

namespace DuoFlow.Vision;

/// <summary>
/// Mean and standard deviation of a luminance buffer, in one pass.
///
/// <para>
/// Shared by both detectors because the number means the same thing in both:
/// "does this frame contain structure at all". M3.2 uses it as a flat-frame
/// guard before Otsu; M3.3 uses it for the same reason plus the trace line.
/// Two implementations of the same statistic would eventually disagree about
/// the ddof or the rounding, and then a probe log would be unreadable.
/// </para>
/// <para>
/// Population standard deviation (divide by N), not sample (N-1): the buffer
/// IS the whole population, and at 160x90 the difference is invisible anyway.
/// </para>
/// </summary>
internal static class GrayStats
{
    internal static (double Mean, double Std) MeanAndStd(ReadOnlySpan<byte> pixels)
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

        // max(0, ...) is not defensive noise: the algebraic form subtracts two
        // nearly equal large numbers, and on a uniform frame rounding can push
        // the result a hair below zero. Sqrt of that is NaN, which then
        // propagates into every comparison as "false" and silently disables a
        // flat-frame guard.
        return (mean, Math.Sqrt(Math.Max(0.0, (sumSq / pixels.Length) - (mean * mean))));
    }
}
