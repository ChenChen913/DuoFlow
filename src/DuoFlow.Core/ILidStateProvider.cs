namespace DuoFlow.Core;

/// <summary>
/// Pluggable lid detection provider contract (PROJECT_SPEC §18).
/// Implementations: ManualProvider (M1.1), CameraProvider (M3),
/// SensorProvider (M4). Selection/priority is the Provider Manager's job (M4).
/// Signature fixed by PROJECT_SPEC §18 - do not rename members or add
/// required members to this interface; extend via new interfaces instead.
/// </summary>
public interface ILidStateProvider
{
    /// <summary>Whether this provider can produce states on this machine.
    /// Must be answered by capability detection, never assumed (DD-025).</summary>
    bool IsAvailable { get; }

    /// <summary>Start producing states (subscribers may then receive
    /// <see cref="StateChanged"/> callbacks).</summary>
    Task StartAsync();

    /// <summary>Stop producing states and release per-run resources.</summary>
    Task StopAsync();

    /// <summary>Raised whenever the provider has a new lid state estimate.
    /// Handlers must be fast and must not block - see DD-002: this is the
    /// only channel inputs are allowed to talk to the rest of the system.</summary>
    event EventHandler<LidState> StateChanged;
}
