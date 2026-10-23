using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M3.1: the pixel &lt;-&gt; normalized transform. This is the single most
/// important piece of the camera path to get right, because everything
/// downstream assumes normalized coordinates: if the transform is wrong by a
/// rotation, screen detection still "works" but detects a rotated screen, and
/// the failure looks like a geometry bug three layers away.
/// </summary>
public class CameraProfileTests
{
    private static CameraProfile Make(int rotation = 0, bool mirror = false, NormalizedRect? crop = null)
        => new()
        {
            SourceWidth = 640,
            SourceHeight = 480,
            RotationDegrees = rotation,
            MirrorHorizontally = mirror,
            Crop = crop ?? NormalizedRect.Full,
        };

    public static TheoryData<int, bool> Orientations()
    {
        var data = new TheoryData<int, bool>();
        foreach (int rotation in new[] { 0, 90, 180, 270 })
        {
            data.Add(rotation, false);
            data.Add(rotation, true);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Orientations))]
    public void RoundTrip_IsExact_ForEveryOrientation(int rotation, bool mirror)
    {
        CameraProfile profile = Make(rotation, mirror);
        var points = new (double X, double Y)[]
        {
            (0, 0), (640, 0), (0, 480), (640, 480),
            (320, 240), (123.5, 77.25), (639, 1),
        };

        foreach ((double x, double y) in points)
        {
            (double u, double v) = profile.ToNormalized(x, y);
            (double bx, double by) = profile.ToSourcePixel(u, v);
            Assert.Equal(x, bx, 9);
            Assert.Equal(y, by, 9);
        }
    }

    [Theory]
    [MemberData(nameof(Orientations))]
    public void RoundTrip_IsExact_WithCrop(int rotation, bool mirror)
    {
        var crop = new NormalizedRect(0.10, 0.20, 0.55, 0.60);
        CameraProfile profile = Make(rotation, mirror, crop);

        for (int i = 0; i <= 10; i++)
        {
            for (int j = 0; j <= 10; j++)
            {
                double u = i / 10.0;
                double v = j / 10.0;
                (double x, double y) = profile.ToSourcePixel(u, v);
                (double bu, double bv) = profile.ToNormalized(x, y);
                Assert.Equal(u, bu, 9);
                Assert.Equal(v, bv, 9);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Orientations))]
    public void FrameCentre_MapsToNormalizedCentre(int rotation, bool mirror)
    {
        // Rotation and mirroring are symmetries about the centre, so the centre
        // is a fixed point of both. If this fails, the transform is not a
        // rotation but a translation - a bug that still round-trips.
        CameraProfile profile = Make(rotation, mirror);
        (double u, double v) = profile.ToNormalized(320, 240);
        Assert.Equal(0.5, u, 12);
        Assert.Equal(0.5, v, 12);
    }

    [Fact]
    public void Rotate90_MapsSourceTopLeft_ToRotatedTopRight()
    {
        // Clockwise rotation: what was the top-left corner ends up top-right.
        // Asserted on a concrete point because the round trip alone would also
        // pass for a 270 degree implementation that inverts both directions.
        CameraProfile profile = Make(90);
        (double u, double v) = profile.ToNormalized(0, 0);
        Assert.Equal(1.0, u, 12);
        Assert.Equal(0.0, v, 12);
    }

    [Fact]
    public void Mirror_FlipsU_AndOnlyU()
    {
        CameraProfile plain = Make(0, mirror: false);
        CameraProfile mirrored = Make(0, mirror: true);

        (double u1, double v1) = plain.ToNormalized(100, 60);
        (double u2, double v2) = mirrored.ToNormalized(100, 60);

        Assert.Equal(1.0 - u1, u2, 12);
        Assert.Equal(v1, v2, 12);
    }

    [Fact]
    public void RotatedSize_SwapsAxes_OnlyForQuarterTurns()
    {
        Assert.Equal((640, 480), Make(0).RotatedSize);
        Assert.Equal((480, 640), Make(90).RotatedSize);
        Assert.Equal((640, 480), Make(180).RotatedSize);
        Assert.Equal((480, 640), Make(270).RotatedSize);
    }

    [Fact]
    public void Crop_NarrowsTheNormalizedRange()
    {
        var crop = new NormalizedRect(0.25, 0.25, 0.5, 0.5);
        CameraProfile profile = Make(0, false, crop);

        // Source pixel at the crop's own top-left maps to normalized (0,0).
        (double u, double v) = profile.ToNormalized(160, 120);
        Assert.Equal(0.0, u, 12);
        Assert.Equal(0.0, v, 12);

        // ...and the crop's bottom-right maps to (1,1).
        (double u2, double v2) = profile.ToNormalized(480, 360);
        Assert.Equal(1.0, u2, 12);
        Assert.Equal(1.0, v2, 12);
    }

    [Fact]
    public void Validate_RejectsImpossibleProfiles()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CameraProfile
        {
            SourceWidth = 0, SourceHeight = 480,
        }.Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() => new CameraProfile
        {
            SourceWidth = 640, SourceHeight = 480, RotationDegrees = 45,
        }.Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() => new CameraProfile
        {
            SourceWidth = 640, SourceHeight = 480,
            Crop = new NormalizedRect(0.8, 0.0, 0.5, 1.0),   // leaves the frame
        }.Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() => new CameraProfile
        {
            SourceWidth = 640, SourceHeight = 480,
            Crop = new NormalizedRect(0.0, 0.0, 0.0, 1.0),   // zero width
        }.Validate());
    }

    [Fact]
    public void For_RefusesADeviceWhoseFormatIsNotNegotiated()
    {
        var device = new CameraDeviceInfo { Id = "x", Name = "cam" };
        Assert.False(device.HasStreamFormat);
        Assert.Throws<InvalidOperationException>(() => CameraProfile.For(device));

        CameraDeviceInfo withFormat = device.WithFormat(1280, 720, 30.0, "NV12");
        Assert.True(withFormat.HasStreamFormat);
        CameraProfile profile = CameraProfile.For(withFormat);
        Assert.Equal(1280, profile.SourceWidth);
        Assert.Equal(720, profile.SourceHeight);
    }
}
