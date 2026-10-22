namespace DuoFlow.Render;

/// <summary>
/// Classification of a failed HRESULT as "the GPU device is gone" (DD-051).
///
/// Kept as plain <c>int</c> constants in the PURE project on purpose: Vortice's
/// <c>Result</c> lives in the App's dependency set, and the point of this rule
/// is that it can be asserted in a unit test without a GPU, a driver, or even
/// Windows. The app converts its <c>Result.Code</c> / <c>Exception.HResult</c>
/// into an int and asks here.
///
/// Removed/Reset are the two documented unrecoverable states: every device
/// object is invalid from that moment and only a full re-create recovers.
/// DriverInternalError is the "TDR while we were presenting" variant that
/// commonly follows them, so it is treated the same way.
/// </summary>
public static class DeviceHealth
{
    /// <summary>DXGI_ERROR_DEVICE_REMOVED (0x887A0005).</summary>
    public const int DeviceRemoved = unchecked((int)0x887A0005);

    /// <summary>DXGI_ERROR_DEVICE_RESET (0x887A0006).</summary>
    public const int DeviceReset = unchecked((int)0x887A0006);

    /// <summary>DXGI_ERROR_DRIVER_INTERNAL_ERROR (0x887A0020).</summary>
    public const int DriverInternalError = unchecked((int)0x887A0020);

    /// <summary>true when the HRESULT means the device has to be re-created.
    /// Success codes and every other failure return false.</summary>
    public static bool IsFatalLoss(int hresult)
        => hresult == DeviceRemoved
        || hresult == DeviceReset
        || hresult == DriverInternalError;
}
