using System;

namespace DuoFlow.Vision;

/// <summary>
/// The two coverage knots that turn a fraction into a progress value, plus the
/// threshold that decides when "the frame went blank" means "the lid closed".
///
/// <para>
/// <b>Why the open knot is not 0.</b> With the lid wide open the camera is
/// looking at the room, and the bottom of that frame still holds a surface -
/// the desk, the floor, the user's chest. Coverage there is not zero, it is
/// some baseline that depends on how the laptop is sitting. Pretending
/// otherwise would clamp the first fifth of the motion to zero and then start
/// moving. The baseline is what <see cref="OpenCoverage"/> is for, and M3.5's
/// three-point calibration is what replaces this default with a measured one.
/// </para>
/// </summary>
public sealed record LidProgressCalibration
{
    public static LidProgressCalibration Default { get; } = new();

    /// <summary>Coverage observed with the lid fully open. Maps to progress 0.</summary>
    public double OpenCoverage { get; init; } = 0.20;

    /// <summary>Coverage observed with the lid closed. Maps to progress 1.</summary>
    public double ClosedCoverage { get; init; } = 0.95;

    /// <summary>
    /// Replace <see cref="OpenCoverage"/> with the first usable measurement.
    ///
    /// <para>
    /// <b>Why this exists, measured on the development laptop.</b> With
    /// <see cref="OpenCoverage"/> at its placeholder 0.20, a fully open lid
    /// measured 0.513 coverage and therefore progress 0.417 - the app would
    /// start with the desktop already 42% folded, which is a visible bug, not a
    /// rounding error. The cause is not the estimator: with the lid wide open
    /// the camera is not looking at the deck at all, it is looking at the room,
    /// and whatever dark thing happens to reach the bottom of that frame is
    /// what gets measured. No constant can be right for every desk, room and
    /// laptop height.
    /// </para>
    /// <para>
    /// The one thing that IS reliably known is that a desktop app starts with
    /// the lid open - if it were shut the user could not have launched it. So
    /// the first usable sample is taken as "this is what open looks like", and
    /// progress starts at 0 by construction. This is a placeholder for M3.5's
    /// three-point calibration, which replaces it with something the user
    /// actually confirmed.
    /// </para>
    /// <para>
    /// Off by default so the pure mapping stays a pure mapping; the camera
    /// provider turns it on because it is the component that runs in a real
    /// session. Only usable samples may set the baseline - a blank frame at
    /// start-up must not become "open".
    /// </para>
    /// </summary>
    public bool LearnOpenBaseline { get; init; }

    /// <summary>
    /// Progress that must have been reached before a blank frame is read as
    /// "closed" rather than "no measurement".
    ///
    /// <para>
    /// This one number is the difference between a lid sensor and a light
    /// switch. A blank frame at the END of a closing motion means the camera is
    /// against the deck. A blank frame at START-UP means the lens is capped,
    /// the room is dark, or the privacy shutter is closed - and treating that
    /// as "closed" would play a full closing animation every time the machine
    /// boots with something over the lens. The pixels are identical; only the
    /// history separates them.
    /// </para>
    /// </summary>
    public double ClosedConfirmProgress { get; init; } = 0.60;

    public void Validate()
    {
        if (!double.IsFinite(OpenCoverage) || !double.IsFinite(ClosedCoverage))
        {
            throw new ArgumentOutOfRangeException(nameof(OpenCoverage), "calibration knots must be finite");
        }

        if (!(ClosedCoverage > OpenCoverage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ClosedCoverage),
                $"ClosedCoverage ({ClosedCoverage}) must exceed OpenCoverage ({OpenCoverage})");
        }

        if (!(ClosedConfirmProgress >= 0.0 && ClosedConfirmProgress <= 1.0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ClosedConfirmProgress), ClosedConfirmProgress, "must be in 0~1");
        }
    }

    /// <summary>Linear map from coverage to progress, clamped to 0~1.</summary>
    public double ToProgress(double coverage) => ToProgress(coverage, OpenCoverage);

    /// <summary>
    /// Same map against an explicit open knot - used when the baseline was
    /// learned at runtime rather than configured.
    /// </summary>
    public double ToProgress(double coverage, double openCoverage)
    {
        if (double.IsNaN(coverage) || double.IsNaN(openCoverage))
        {
            return 0.0;
        }

        double span = ClosedCoverage - openCoverage;

        // A learned baseline can land on (or above) the closed knot if the app
        // happened to start while the lid was already down. There is no span to
        // map onto then, and the safe answer is "no evidence of movement" (0)
        // rather than dividing by ~0 - reporting 1.0 here would claim the lid
        // is shut on the strength of a calibration that has no range at all.
        if (!(span > 1e-6))
        {
            return 0.0;
        }

        return Math.Clamp((coverage - openCoverage) / span, 0.0, 1.0);
    }
}

/// <summary>Why a progress estimate ended the way it did.</summary>
public enum LidProgressReason
{
    /// <summary>A usable coverage measurement drove this value.</summary>
    Measured = 0,

    /// <summary>Nothing usable this frame; <see cref="LidProgressEstimate.Progress"/> is the last known value.</summary>
    NoMeasurement = 1,

    /// <summary>Frame went blank after progress had already climbed - the lid is shut.</summary>
    ClosedConfirmed = 2,
}

/// <summary>
/// One progress sample. This is what <c>CameraLidProvider</c> turns into a
/// <c>LidState</c>; it lives here, without a Core reference, so the whole
/// mapping can be unit tested against synthetic coverage values.
/// </summary>
public sealed record LidProgressEstimate
{
    public required LidProgressReason Reason { get; init; }

    /// <summary>0 = fully open, 1 = closed (PROJECT_SPEC §6 semantics).</summary>
    public double Progress { get; init; }

    /// <summary>How much this sample should be trusted, 0..1.</summary>
    public double Confidence { get; init; }

    /// <summary>The coverage this sample was derived from, 0..1.</summary>
    public double Coverage { get; init; }

    /// <summary>False when the sample carries no new information.</summary>
    public bool HasMeasurement => Reason != LidProgressReason.NoMeasurement;

    public bool IsClosed => Reason == LidProgressReason.ClosedConfirmed || Progress >= 0.999;

    public string Describe()
        => $"{Reason} progress={Progress:0.000} conf={Confidence:0.000} coverage={Coverage:0.000}";
}

/// <summary>
/// M3.3 stage 2: coverage samples in, progress out.
///
/// <para>
/// <b>Stateful on purpose, and the state is exactly one bit plus one number.</b>
/// A stateless mapping cannot distinguish "the frame went blank because the lid
/// just closed" from "the frame was already blank when we started", and those
/// two need opposite answers (see
/// <see cref="LidProgressCalibration.ClosedConfirmProgress"/>). Everything else
/// here is a pure function of the current sample.
/// </para>
/// <para>
/// <b>What it does not do: smoothing.</b> No filtering, no rate limiting, no
/// outlier rejection - M3.4 owns that, and mixing it in here would make both
/// untestable. A tracker fed a jumping coverage series will produce a jumping
/// progress series, which is the correct division of labour and the reason
/// M3.4 exists.
/// </para>
/// </summary>
public sealed class LidProgressTracker
{
    private readonly LidProgressCalibration _calibration;

    private double _openBaseline;
    private bool _baselineLearned;
    private double _progress;
    private bool _closedLatched;

    public LidProgressTracker(LidProgressCalibration? calibration = null)
    {
        _calibration = calibration ?? LidProgressCalibration.Default;
        _calibration.Validate();
        _openBaseline = _calibration.OpenCoverage;
    }

    public LidProgressCalibration Calibration => _calibration;

    /// <summary>
    /// The open knot actually in use: <see cref="LidProgressCalibration.OpenCoverage"/>
    /// until a sample is learned, then whatever was learned. Exposed for the
    /// trace, because "why did progress start at 0.4" is answered here.
    /// </summary>
    public double OpenBaseline => _openBaseline;

    /// <summary>True once a usable sample has set <see cref="OpenBaseline"/>.</summary>
    public bool HasLearnedBaseline => _baselineLearned;

    /// <summary>Last progress emitted. Starts at 0 (fully open).</summary>
    public double Progress => _progress;

    /// <summary>True once a blank frame has been accepted as "the lid is shut".</summary>
    public bool IsClosedLatched => _closedLatched;

    public LidProgressEstimate Update(DeckCoverage deck)
    {
        ArgumentNullException.ThrowIfNull(deck);

        if (deck.Reason == DeckCoverageReason.FlatFrame)
        {
            if (_progress >= _calibration.ClosedConfirmProgress)
            {
                _closedLatched = true;
                _progress = 1.0;
                return new LidProgressEstimate
                {
                    Reason = LidProgressReason.ClosedConfirmed,
                    Progress = 1.0,
                    Confidence = 1.0,
                    Coverage = deck.Coverage,
                };
            }

            // Blank frame with no closing history: a capped lens, a dark room,
            // a closed privacy shutter. Saying "closed" here would fire the
            // whole closing animation at start-up.
            return Frozen(deck);
        }

        if (!deck.IsUsable)
        {
            return Frozen(deck);
        }

        // Learn the open baseline from the FIRST usable sample only. A blank
        // frame cannot get here (handled above), which is deliberate: capping
        // the lens at start-up must not teach the tracker that "open" is
        // coverage 1.0.
        if (_calibration.LearnOpenBaseline && !_baselineLearned)
        {
            _openBaseline = deck.Coverage;
            _baselineLearned = true;
        }

        double progress = _calibration.ToProgress(deck.Coverage, _openBaseline);

        // Re-opening: a real measurement says we are no longer near closed, so
        // the latch must drop. Without this the provider could confirm "closed"
        // once and then treat every later blank frame as closed forever, even
        // after the user opened the lid again and capped the lens.
        if (_closedLatched && progress < _calibration.ClosedConfirmProgress)
        {
            _closedLatched = false;
        }

        _progress = progress;

        return new LidProgressEstimate
        {
            Reason = LidProgressReason.Measured,
            Progress = progress,
            Confidence = deck.Confidence,
            Coverage = deck.Coverage,
        };
    }

    private LidProgressEstimate Frozen(DeckCoverage deck) => new()
    {
        Reason = LidProgressReason.NoMeasurement,
        Progress = _progress,
        Confidence = 0.0,
        Coverage = deck.Coverage,
    };
}
