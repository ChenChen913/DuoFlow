namespace DuoFlow.Core;

/// <summary>
/// Unified lid state snapshot - the single data contract between every input
/// provider and the rest of the system (DD-002: all inputs become LidState,
/// the renderer only consumes Progress).
/// Signature fixed by PROJECT_SPEC §5 - do not rename fields or parameters.
/// </summary>
/// <param name="Angle">Current estimated lid angle in degrees.</param>
/// <param name="Progress">
/// Normalized open/close progress, 0~1 (DD-003 / PROJECT_SPEC §6):
/// 0.0 = fully open, 1.0 = near fully closed (NOT "fully closed").
/// Device-independent by design; per-device calibration happens upstream.
/// </param>
/// <param name="Velocity">Open/close change velocity (M1.3 Animation Engine
/// computes and consumes it; providers may emit 0 for now).</param>
/// <param name="Confidence">Trustworthiness of this sample, 0~1.
/// Manual input is 1.0 (user is in control, HARDWARE_COMPATIBILITY §4.3).</param>
/// <param name="Source">Which provider produced this state.</param>
public sealed record LidState(
    double Angle,
    double Progress,
    double Velocity,
    double Confidence,
    LidStateSource Source);
