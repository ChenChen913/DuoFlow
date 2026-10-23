namespace DuoFlow.Vision;

/// <summary>
/// Why the camera path is not working. Deliberately coarser than an HRESULT
/// and deliberately closed: each value maps to ONE thing a user can do.
/// </summary>
public enum CameraFailure
{
    /// <summary>No failure - the camera is usable.</summary>
    None = 0,

    /// <summary>No capture device at all (or it disappeared).</summary>
    NoDevice = 1,

    /// <summary>Device exists but the app may not open it (privacy setting / policy).</summary>
    AccessDenied = 2,

    /// <summary>Another process holds the device in an incompatible sharing mode.</summary>
    InUse = 3,

    /// <summary>Device node exists but is disabled in Device Manager.</summary>
    Disabled = 4,

    /// <summary>Device opened but does not offer a color stream we can read.</summary>
    Unsupported = 5,

    Unknown = 6,
}

/// <summary>
/// Turns platform failures into <see cref="CameraFailure"/> plus a sentence the
/// console can show.
///
/// <para>
/// Why this is not just <c>ex.HResult.ToString("X")</c>: an HRESULT tells the
/// person reading the trace nothing, and M3.6 requires every failure to degrade
/// gracefully - which is impossible if the failure is an opaque number. The
/// mapping is intentionally conservative: anything unrecognised becomes
/// <see cref="CameraFailure.Unknown"/> rather than being forced into a
/// plausible-looking bucket that would then get the wrong advice.
/// </para>
/// </summary>
public static class CameraHealth
{
    // The few HRESULTs that actually occur on this path, spelled out because
    // a bare hex literal in a switch is unreadable six months later.
    private const int E_ACCESSDENIED = unchecked((int)0x80070005);
    private const int ERROR_NOT_SUPPORTED = unchecked((int)0x80070032);
    private const int ERROR_DEVICE_DISABLED = unchecked((int)0x8007001B);
    private const int ERROR_BUSY = unchecked((int)0x800700AA);
    private const int ERROR_NOT_FOUND = unchecked((int)0x80070490);
    private const int ERROR_DEVICE_NOT_CONNECTED = unchecked((int)0x8007048F);
    private const int ERROR_DEVICE_NOT_AVAILABLE = unchecked((int)0x80070496);

    public static CameraFailure Classify(Exception? exception)
    {
        if (exception is null)
        {
            return CameraFailure.None;
        }

        switch (exception)
        {
            case UnauthorizedAccessException:
                return CameraFailure.AccessDenied;
            case DllNotFoundException:
            case EntryPointNotFoundException:
                return CameraFailure.Unsupported;
            case FileNotFoundException:
                return CameraFailure.NoDevice;
        }

        CameraFailure byHResult = FromHResult(exception.HResult);
        return byHResult != CameraFailure.Unknown
            ? byHResult
            : exception.InnerException is { } inner
                ? Classify(inner)
                : CameraFailure.Unknown;
    }

    public static CameraFailure FromHResult(int hresult) => hresult switch
    {
        E_ACCESSDENIED => CameraFailure.AccessDenied,
        ERROR_DEVICE_DISABLED => CameraFailure.Disabled,
        ERROR_DEVICE_NOT_CONNECTED => CameraFailure.NoDevice,
        ERROR_DEVICE_NOT_AVAILABLE => CameraFailure.NoDevice,
        ERROR_NOT_FOUND => CameraFailure.NoDevice,
        ERROR_BUSY => CameraFailure.InUse,
        ERROR_NOT_SUPPORTED => CameraFailure.Unsupported,
        _ => CameraFailure.Unknown,
    };

    /// <summary>What the user can actually do about it.</summary>
    public static string Describe(CameraFailure failure) => failure switch
    {
        CameraFailure.None => "摄像头可用",
        CameraFailure.NoDevice => "没有检测到摄像头（或设备已断开）",
        CameraFailure.AccessDenied => "摄像头被系统隐私设置拦截：设置 → 隐私和安全性 → 摄像头 → 允许桌面应用访问",
        CameraFailure.InUse => "摄像头被其他程序占用（会议软件 / 相机应用），关掉后重试",
        CameraFailure.Disabled => "摄像头在设备管理器中被禁用，启用后重试",
        CameraFailure.Unsupported => "摄像头不支持可读的彩色视频流",
        _ => "摄像头未知错误，详见 trace",
    };

    /// <summary>
    /// What the pipeline should do about it: every camera failure degrades to
    /// Manual rather than killing the effect (HARDWARE §1.1 #6).
    /// </summary>
    public static bool IsRecoverable(CameraFailure failure) => failure switch
    {
        CameraFailure.None => true,
        // A device that is merely busy or blocked can come back without the
        // process restarting; a missing one is also retried, because laptops
        // re-enumerate cameras on resume.
        CameraFailure.InUse => true,
        CameraFailure.AccessDenied => true,
        CameraFailure.NoDevice => true,
        _ => false,
    };
}
