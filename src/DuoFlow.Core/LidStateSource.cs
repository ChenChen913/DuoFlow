namespace DuoFlow.Core;

/// <summary>
/// Where a <see cref="LidState"/> sample came from (PROJECT_SPEC §5).
/// Members and order are fixed by the spec - do not rename or reorder.
/// </summary>
public enum LidStateSource
{
    Manual,
    Camera,
    Sensor,
    Unknown
}
