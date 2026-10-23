namespace DuoFlow.Vision;

/// <summary>
/// Where the camera physically sits relative to the display. M3.1 uses it only
/// to pick a sensible default device (a laptop lid camera is "Front"), but the
/// value is also the first hint a later stage gets about the geometry: a front
/// camera sees the screen edge-on-ish, an external webcam on a stand sees it
/// from the side. Never a hard assumption - just a prior (HARDWARE §1.1 #1).
/// </summary>
public enum CameraEnclosure
{
    Unknown = 0,
    Front = 1,
    Back = 2,
    External = 3,
}

/// <summary>
/// One enumerated capture device, as far as we know it WITHOUT opening it.
///
/// <para>
/// <see cref="Width"/>/<see cref="Height"/>/<see cref="FrameRate"/> are 0 until
/// the device has actually been opened: on Windows the stream format is a
/// property of the negotiated source, not of the device node, and pretending
/// otherwise is how "1920x1080" ends up hardcoded into a machine that reports
/// 1280x720. Treat 0 as "unknown", not as "zero".
/// </para>
/// <para>
/// <see cref="RotationDegrees"/> is the correction that maps the frame's own
/// pixel grid onto upright space, in 90 degree steps clockwise. Most laptop
/// cameras report 0; the field exists so a rotated sensor does not silently
/// become a rotated screen detection.
/// </para>
/// </summary>
public sealed record CameraDeviceInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public CameraEnclosure Enclosure { get; init; } = CameraEnclosure.Unknown;

    /// <summary>false when the device node exists but is disabled in Device Manager.</summary>
    public bool IsEnabled { get; init; } = true;

    public int Width { get; init; }

    public int Height { get; init; }

    public double FrameRate { get; init; }

    /// <summary>FourCC / subtype as reported by the platform ("MJPG", "NV12", "BGRA8"...).</summary>
    public string PixelFormat { get; init; } = "";

    public int RotationDegrees { get; init; }

    public bool IsMirrored { get; init; }

    /// <summary>True once a stream has been negotiated, i.e. the three
    /// resolution/fps/format fields above mean something.</summary>
    public bool HasStreamFormat => Width > 0 && Height > 0;

    public CameraDeviceInfo WithFormat(int width, int height, double frameRate, string pixelFormat)
        => this with { Width = width, Height = height, FrameRate = frameRate, PixelFormat = pixelFormat };
}
