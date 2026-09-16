namespace DuoFlow.Core;

/// <summary>
/// Manual lid state provider (DD-004): the UI drives Progress directly
/// (M1.2 slider -> SetProgress -> StateChanged -> render pipeline), so the
/// visual pipeline can be developed and verified before any real
/// Camera/Sensor provider exists.
/// </summary>
/// <remarks>
/// Semantics fixed for M1.1 (DD-035):
///  - <see cref="SetProgress"/> clamps to 0~1, stores the value and raises
///    <see cref="StateChanged"/> synchronously on the calling thread,
///    regardless of whether <see cref="StartAsync"/> was called.
///  - Every raised state has Source = <see cref="LidStateSource.Manual"/>,
///    Confidence = 1.0 (user is in control, HARDWARE_COMPATIBILITY §4.3)
///    and Velocity = 0 (velocity is the M1.3 Animation Engine's job,
///    PROJECT_SPEC §7 pipeline).
///  - Angle uses the PROJECT_SPEC §6 illustrative linear mapping
///    (180° -> 0.0, 30° -> 1.0) as a placeholder default. This is NOT a
///    hardware claim; per-device calibration (M3.5 / M4) replaces it.
///  - Not thread-safe by design in M1.1 (UI-thread usage); the M4 Provider
///    Manager is responsible for serializing provider access.
/// </remarks>
public sealed class ManualProvider : ILidStateProvider
{
    /// <summary>Spec §6 illustrative endpoints for the default angle map:
    /// progress 0.0 -> 180° (fully open), progress 1.0 -> 30° (near closed).</summary>
    public const double DefaultOpenAngleDegrees = 180.0;
    public const double DefaultNearClosedAngleDegrees = 30.0;

    private double _progress;

    /// <summary>Manual input is always available (DD-004) - it has no
    /// hardware dependency to detect.</summary>
    public bool IsAvailable => true;

    /// <summary>True between <see cref="StartAsync"/> and
    /// <see cref="StopAsync"/> (lifecycle flag for the future M4 Provider
    /// Manager; it does not gate <see cref="SetProgress"/>).</summary>
    public bool IsRunning { get; private set; }

    /// <summary>The state that would be (re-)raised right now. Pull-based
    /// counterpart of <see cref="StateChanged"/> - useful for late
    /// subscribers that missed earlier events.</summary>
    public LidState CurrentState => CreateState();

    /// <inheritdoc />
    public event EventHandler<LidState>? StateChanged;

    /// <summary>Drives the provider from the UI (M1.2 slider). Clamps to
    /// 0~1 and immediately raises <see cref="StateChanged"/> with the new
    /// state. Re-setting the same value raises again by design - manual
    /// input is authoritative and downstream must stay in sync.</summary>
    public void SetProgress(double progress)
    {
        if (double.IsNaN(progress))
        {
            throw new ArgumentOutOfRangeException(nameof(progress), progress,
                "Progress must be a number in 0~1 (NaN is not a valid lid position).");
        }

        _progress = Math.Clamp(progress, 0.0, 1.0);
        StateChanged?.Invoke(this, CreateState());
    }

    /// <inheritdoc />
    /// <remarks>Emits the current state once so subscribers immediately
    /// have a baseline (initial Progress = 0 = fully open).</remarks>
    public Task StartAsync()
    {
        IsRunning = true;
        StateChanged?.Invoke(this, CreateState());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        IsRunning = false;
        return Task.CompletedTask;
    }

    private LidState CreateState() => new(
        Angle: DefaultOpenAngleDegrees
               - (DefaultOpenAngleDegrees - DefaultNearClosedAngleDegrees) * _progress,
        Progress: _progress,
        Velocity: 0.0,
        Confidence: 1.0,
        Source: LidStateSource.Manual);
}
