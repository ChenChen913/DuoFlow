namespace DuoFlow.Vision;

/// <summary>A rectangle in normalized 0..1 camera space.</summary>
public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
    /// <summary>The whole frame - the crop most cameras need.</summary>
    public static readonly NormalizedRect Full = new(0.0, 0.0, 1.0, 1.0);

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public void Validate()
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y) ||
            !double.IsFinite(Width) || !double.IsFinite(Height))
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "crop contains NaN or Infinity");
        }
        if (Width <= 0.0 || Height <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), $"crop must have positive size ({Width}x{Height})");
        }
        if (X < -1e-9 || Y < -1e-9 || Right > 1.0 + 1e-9 || Bottom > 1.0 + 1e-9)
        {
            throw new ArgumentOutOfRangeException(nameof(X), $"crop [{X},{Y},{Width},{Height}] leaves the frame");
        }
    }
}

/// <summary>
/// The ONE place where camera pixels are allowed to exist (HARDWARE §1.1 #4).
///
/// <para>
/// A camera hands us a pixel grid whose width, height, orientation, mirroring
/// and usable region are all device-specific. Every downstream stage (screen
/// detection, angle estimation) works in normalized 0..1 coordinates, so those
/// five facts have to be resolved exactly once, here, and in a fixed order:
/// </para>
/// <code>
///   source pixel --rotate--> rotated pixel --mirror--> --crop+normalize--> (u,v)
/// </code>
/// <para>
/// The order is not arbitrary. Rotating AFTER cropping would need a crop
/// rectangle expressed in the rotated frame (which the platform does not give
/// us), and mirroring before rotation would flip the axis the rotation is
/// defined about. Fixing the order also makes the transform invertible, which
/// is what the round-trip tests assert: a point mapped to normalized space and
/// back must land where it started, for all 8 orientation combinations.
/// </para>
/// <para>
/// Coordinates are CONTINUOUS (0..W, 0..H), not pixel indices: the corners of
/// the frame are exactly 0 and W. Pixel indices would introduce a half-pixel
/// offset that differs between rotations, and the round trip would then only
/// be exact for the 0 degree case.
/// </para>
/// </summary>
public sealed record CameraProfile
{
    public required int SourceWidth { get; init; }

    public required int SourceHeight { get; init; }

    /// <summary>Clockwise correction applied before mirroring: 0/90/180/270.</summary>
    public int RotationDegrees { get; init; }

    public bool MirrorHorizontally { get; init; }

    public NormalizedRect Crop { get; init; } = NormalizedRect.Full;

    /// <summary>Frame size after rotation (what the normalized axes span).</summary>
    public (int Width, int Height) RotatedSize
        => RotationDegrees is 90 or 270
            ? (SourceHeight, SourceWidth)
            : (SourceWidth, SourceHeight);

    public static CameraProfile For(CameraDeviceInfo device, NormalizedRect? crop = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (!device.HasStreamFormat)
        {
            throw new InvalidOperationException(
                $"camera '{device.Name}' has no negotiated stream format yet (width/height are 0)");
        }

        return new CameraProfile
        {
            SourceWidth = device.Width,
            SourceHeight = device.Height,
            RotationDegrees = device.RotationDegrees,
            MirrorHorizontally = device.IsMirrored,
            Crop = crop ?? NormalizedRect.Full,
        };
    }

    public void Validate()
    {
        if (SourceWidth <= 0 || SourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SourceWidth), $"source size {SourceWidth}x{SourceHeight}");
        }
        if (RotationDegrees is not (0 or 90 or 180 or 270))
        {
            throw new ArgumentOutOfRangeException(nameof(RotationDegrees), $"rotation must be 0/90/180/270, got {RotationDegrees}");
        }
        Crop.Validate();
    }

    /// <summary>Source pixel coordinate -> normalized (u,v), u right, v down.</summary>
    public (double U, double V) ToNormalized(double pixelX, double pixelY)
    {
        Validate();
        (int rw, int rh) = RotatedSize;

        // 1. rotate (clockwise)
        (double rx, double ry) = RotationDegrees switch
        {
            90 => (rw - pixelY, pixelX),
            180 => (SourceWidth - pixelX, SourceHeight - pixelY),
            270 => (pixelY, SourceWidth - pixelX),
            _ => (pixelX, pixelY),
        };

        // 2. mirror in the rotated frame
        if (MirrorHorizontally)
        {
            rx = rw - rx;
        }

        // 3. crop, then normalize
        double u0 = rx / rw;
        double v0 = ry / rh;
        return ((u0 - Crop.X) / Crop.Width, (v0 - Crop.Y) / Crop.Height);
    }

    /// <summary>Inverse of <see cref="ToNormalized"/> - the only way back to pixels.</summary>
    public (double X, double Y) ToSourcePixel(double u, double v)
    {
        Validate();
        (int rw, int rh) = RotatedSize;

        double u0 = Crop.X + u * Crop.Width;
        double v0 = Crop.Y + v * Crop.Height;
        double rx = u0 * rw;
        double ry = v0 * rh;

        if (MirrorHorizontally)
        {
            rx = rw - rx;
        }

        return RotationDegrees switch
        {
            90 => (ry, rw - rx),
            180 => (SourceWidth - rx, SourceHeight - ry),
            270 => (SourceWidth - ry, rx),
            _ => (rx, ry),
        };
    }

    /// <summary>One line for the trace: enough to reproduce a mis-normalized frame.</summary>
    public string Describe()
        => $"{SourceWidth}x{SourceHeight} rot={RotationDegrees} mirror={MirrorHorizontally} " +
           $"crop=({Crop.X:0.###},{Crop.Y:0.###},{Crop.Width:0.###},{Crop.Height:0.###})";
}
