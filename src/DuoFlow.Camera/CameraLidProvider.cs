using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuoFlow.Core;
using DuoFlow.Vision;

namespace DuoFlow.Camera;

/// <summary>
/// M3.3: the camera as a lid-state provider.
///
/// <para>
/// <b>What it measures.</b> Not an angle. It measures how much of the camera
/// frame is occupied by the near surface below it
/// (<see cref="DeckCoverageEstimator"/>), turns that into a 0~1 progress
/// (<see cref="LidProgressTracker"/>), and reports it as a
/// <see cref="LidState"/>. The chain is monotone in lid angle, which is all the
/// renderer ever needed (DD-002: the renderer consumes Progress, never Angle).
/// </para>
/// <para>
/// <b><see cref="LidState.Angle"/> is a NOMINAL value, not a measurement.</b>
/// A coverage fraction cannot be inverted into degrees without knowing the
/// camera's mounting geometry and field of view, and HARDWARE §1.1 forbids
/// assuming either. So Angle uses the same illustrative linear map
/// <see cref="ManualProvider"/> already uses (180° open, 30° near-closed) and
/// says so; the honest number is <see cref="LidState.Confidence"/>. Reporting a
/// plausible-looking angle that was actually invented would be worse than
/// reporting none, because someone would eventually calibrate against it.
/// </para>
/// <para>
/// <b>Why this class lives in DuoFlow.Camera.</b> It is an adapter: device
/// frames in, domain contract out. <c>DuoFlow.Vision</c> must stay free of
/// <c>DuoFlow.Core</c> so the algorithms keep zero knowledge of the app's
/// contracts - which is why the progress model returns
/// <see cref="LidProgressEstimate"/> rather than <see cref="LidState"/>, and why
/// this project now references Core (DD-055).
/// </para>
/// <para>
/// <b>Degradation is M3.6's job, not this class's.</b> When the camera is
/// missing, busy or producing nothing, this provider reports
/// <see cref="IsAvailable"/> = false and records why in
/// <see cref="LastError"/>. It does not silently invent progress and it does not
/// throw into the caller's thread - the Provider Manager (M4) is what decides
/// who takes over.
/// </para>
/// </summary>
public sealed class CameraLidProvider : ILidStateProvider, IAsyncDisposable
{
    private readonly MediaCaptureCamera _camera;
    private readonly DeckCoverageOptions _deckOptions;
    private readonly LidProgressTracker _tracker;

    private bool _running;

    public CameraLidProvider(
        double targetFps = MediaCaptureCamera.DefaultTargetFps,
        DeckCoverageOptions? deckOptions = null,
        LidProgressCalibration? calibration = null)
    {
        _camera = new MediaCaptureCamera(targetFps);
        _deckOptions = deckOptions ?? DeckCoverageOptions.Default;

        // Learn the open knot from the first usable frame unless the caller
        // supplied a calibration they measured themselves. Without this a
        // fully open lid reads as ~0.42 progress on the development laptop,
        // because the placeholder open knot cannot know what the room looks
        // like from this particular camera (see LearnOpenBaseline).
        _tracker = new LidProgressTracker(
            calibration ?? (LidProgressCalibration.Default with { LearnOpenBaseline = true }));

        _camera.FrameArrived += OnFrameArrived;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Answered by capability detection, never assumed (DD-025): true only
    /// while a camera is actually open and streaming. Before
    /// <see cref="StartAsync"/> there is nothing to claim, so this is false.
    /// </remarks>
    public bool IsAvailable => _camera.IsRunning;

    /// <inheritdoc />
    public event EventHandler<LidState>? StateChanged;

    /// <summary>The most recent state raised, for late subscribers.</summary>
    public LidState? CurrentState { get; private set; }

    /// <summary>The most recent raw measurement - the diagnostic half of the story.</summary>
    public DeckCoverage? LastCoverage { get; private set; }

    /// <summary>The most recent mapped progress sample.</summary>
    public LidProgressEstimate? LastEstimate { get; private set; }

    /// <summary>The open knot in use - the learned baseline once one exists.</summary>
    public double OpenBaseline => _tracker.OpenBaseline;

    /// <summary>True once a usable frame has fixed the open baseline.</summary>
    public bool HasLearnedBaseline => _tracker.HasLearnedBaseline;

    /// <summary>Frames that reached the estimator.</summary>
    public long FramesProcessed { get; private set; }

    /// <summary>Frames the vision chain threw on. Must stay 0.</summary>
    public long FrameErrors { get; private set; }

    /// <summary>Last failure text, or null. For the UI to explain, not to parse.</summary>
    public string? LastError { get; private set; }

    /// <summary>The camera that ended up streaming, once started.</summary>
    public CameraDeviceInfo? Device => _camera.Device;

    /// <inheritdoc />
    /// <remarks>
    /// Never throws for "no camera": that is a normal machine state and M3.6
    /// classifies it. It records the reason and leaves
    /// <see cref="IsAvailable"/> false, so a caller can tell "started" from
    /// "started and immediately found nothing".
    /// </remarks>
    public async Task StartAsync()
    {
        if (_running)
        {
            return;
        }

        try
        {
            IReadOnlyList<CameraDeviceInfo> devices = await MediaCaptureCamera.EnumerateAsync();

            // CameraSelection already prefers the "Front" enclosure, which is
            // the lid camera on every laptop that reports it - and the lid
            // camera is the one whose optical axis moves with the lid. An
            // external webcam on a stand would be a perfectly good camera that
            // measures nothing.
            CameraDeviceInfo? chosen = CameraSelection.Choose(devices);
            if (chosen is null)
            {
                LastError = "no capture device enumerated";
                return;
            }

            await _camera.OpenAsync(chosen.Id, _camera.TargetFps);
            _running = true;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        _running = false;
        await _camera.CloseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _camera.FrameArrived -= OnFrameArrived;
        _running = false;
        await _camera.CloseAsync();
        await _camera.DisposeAsync();
    }

    /// <summary>
    /// Runs on the frame-reader thread.
    ///
    /// <para>
    /// Every exception is swallowed into <see cref="FrameErrors"/> and
    /// <see cref="LastError"/>: this callback sits on a WinRT event, and an
    /// exception thrown here does not propagate to anything that could handle
    /// it - it tears down the frame reader and the camera goes quiet with no
    /// explanation. Counting it keeps "the provider stopped producing" from
    /// being invisible.
    /// </para>
    /// </summary>
    private void OnFrameArrived(CameraFrame frame)
    {
        try
        {
            // The buffer is only valid inside this callback (MediaCaptureCamera
            // reuses it), so everything happens here and only scalars escape.
            GrayImage gray = GrayImage.FromBgra(frame.Width, frame.Height, frame.Bgra.Span);
            DeckCoverage coverage = DeckCoverageEstimator.Estimate(gray, _deckOptions);
            LidProgressEstimate estimate = _tracker.Update(coverage);

            LastCoverage = coverage;
            LastEstimate = estimate;
            FramesProcessed++;

            var state = new LidState(
                Angle: NominalAngle(estimate.Progress),
                Progress: estimate.Progress,
                Velocity: 0.0,
                Confidence: estimate.Confidence,
                Source: LidStateSource.Camera);

            CurrentState = state;
            StateChanged?.Invoke(this, state);
        }
        catch (Exception ex)
        {
            FrameErrors++;
            LastError = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>
    /// Nominal degrees for a progress value, reusing
    /// <see cref="ManualProvider"/>'s illustrative endpoints so the two
    /// providers cannot disagree about what progress 0.5 "means" in degrees.
    ///
    /// Not a measurement. See the class remarks.
    /// </summary>
    public static double NominalAngle(double progress)
    {
        double p = double.IsNaN(progress) ? 0.0 : Math.Clamp(progress, 0.0, 1.0);
        return ManualProvider.DefaultOpenAngleDegrees
               - ((ManualProvider.DefaultOpenAngleDegrees - ManualProvider.DefaultNearClosedAngleDegrees) * p);
    }
}
