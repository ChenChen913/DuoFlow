using System;

namespace DuoFlow.Vision;

/// <summary>
/// The two curve shapes every confidence score in this project is built from.
///
/// <para>
/// <b>Why they are shared rather than copied per detector.</b> M3.2 had them as
/// private helpers inside <see cref="ScreenDetector"/>; M3.3 needs the same
/// shapes with different breakpoints. Two copies of a numeric helper drift -
/// one gets a tweak during a bug hunt and the other does not - and then "the
/// window is 1.0 inside the plausible range" stops being a statement about
/// anything. The breakpoints stay at the call sites, where the physical
/// meaning lives; only the shape is shared.
/// </para>
/// <para>
/// Both return a value in 0..1 and both are total: NaN in gives 0.0 out,
/// never an exception. A score is allowed to be bad, never undefined.
/// </para>
/// </summary>
internal static class ScoreShapes
{
    /// <summary>
    /// Trapezoid: 0 below <paramref name="zero"/>, ramps to 1.0 at
    /// <paramref name="one"/>, holds 1.0 through <paramref name="oneHigh"/>,
    /// falls back to 0.0 at <paramref name="zeroHigh"/>.
    ///
    /// Used for "this measurement is plausible in the middle and meaningless at
    /// the extremes" - aspect ratio, edge contrast, region size.
    /// </summary>
    internal static double Window(double value, double zero, double one, double oneHigh, double zeroHigh)
    {
        if (double.IsNaN(value))
        {
            return 0.0;
        }

        if (value < zero || value > zeroHigh)
        {
            return 0.0;
        }

        if (value < one)
        {
            return (value - zero) / (one - zero);
        }

        if (value <= oneHigh)
        {
            return 1.0;
        }

        return (zeroHigh - value) / (zeroHigh - oneHigh);
    }

    /// <summary>
    /// 1.0 up to <paramref name="plateauEnd"/>, then linearly down to 0.0 at
    /// <paramref name="zeroAt"/>, and 0.0 at or beyond it.
    ///
    /// Used as a veto on "the candidate is basically the whole frame". Such a
    /// region has no measurable boundary - its edge contrast is whatever noise
    /// sits between one frame border and another - yet it scores a perfect
    /// rectangularity because it IS a rectangle.
    /// </summary>
    internal static double HighTaper(double value, double plateauEnd, double zeroAt)
    {
        if (double.IsNaN(value) || value >= zeroAt)
        {
            return 0.0;
        }

        if (value <= plateauEnd)
        {
            return 1.0;
        }

        return (zeroAt - value) / (zeroAt - plateauEnd);
    }
}
