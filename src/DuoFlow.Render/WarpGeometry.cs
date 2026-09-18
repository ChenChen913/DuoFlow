using System;

namespace DuoFlow.Render;

/// <summary>
/// M1.4 Pass-1 geometry knobs (DD-039). PROJECT_SPEC §10 leaves the concrete
/// values to visual tuning ("具体参数必须通过实际视觉测试确定"); these defaults
/// are the M1.4 starting point chosen for the 480x270 preview panel.
/// </summary>
public sealed class WarpOptions
{
    public const double DefaultMaxFoldDegrees = 90.0;
    public const double DefaultCameraDistanceRatio = 2.5;
    public const double DefaultEdgeFeather = 0.004;

    /// <summary>Fold angle reached at progress 1.0, in degrees. Restricted to
    /// (0, 90]: past 90° the plane folds beyond edge-on and its front face
    /// turns away from the viewer (a different visual, M2 territory).</summary>
    public double MaxFoldDegrees { get; init; } = DefaultMaxFoldDegrees;

    /// <summary>Pinhole camera distance from the hinge, in units of the
    /// screen height. Smaller = stronger perspective (more dramatic trapezoid).
    /// Must be &gt; 1.0: at progress 1 a tilt-toward fold puts the far edge
    /// exactly at the camera plane when r = 1, which has no image.</summary>
    public double CameraDistanceRatio { get; init; } = DefaultCameraDistanceRatio;

    /// <summary>true = the far edge tilts AWAY from the viewer: the projected
    /// quad stays inside the frame, narrowing toward the far edge (a monitor
    /// falling back on its stand). false = the far edge tilts TOWARD the
    /// viewer (a laptop lid closing onto you): the far edge widens past the
    /// frame and the viewport clips it. One parameter, verified both ways
    /// (DD-039); the artistic direction is finalized in M2.3 visual tuning.</summary>
    public bool TiltAwayFromViewer { get; init; } = true;

    /// <summary>Which screen edge the hinge sits on. PROJECT_SPEC §11 makes
    /// the hinge position configurable; from M1.5 on the Hinge Mask shares
    /// this knob so mask and warp fold around the same line.</summary>
    public bool HingeAtTop { get; init; }

    /// <summary>Alpha feather width at the projected quad boundary, in
    /// normalized (0~1) frame units - hides the aliasing edge of the fold
    /// against the live desktop. 0 disables the feather (hard edge).</summary>
    public double EdgeFeather { get; init; } = DefaultEdgeFeather;
}

/// <summary>
/// One frame of M1.4 warp output: the constant-buffer payload for
/// shaders/DuoWarp.hlsl plus the projected-quad metrics the unit tests and
/// the real-machine grid verification assert against (DD-039).
///
/// The fold is modeled as a plane hinged along one screen edge rotating
/// around that hinge, seen through a pinhole camera at distance r (in
/// screen-height units). The projected image is a true homography - NOT a
/// scale: the far edge narrows (tilt-away) or widens (tilt-toward)
/// nonlinearly while the whole image compresses toward the hinge.
///
/// Working in the hinge frame (x across [-1,1], y from the hinge [0,1] on
/// both sides), the pixel shader evaluates the INVERSE map per pixel:
///
///     (sx, sy) = (M00·x, M11·y) / (M00 + M21·y)
///
/// with M00 = r·cos(phi), M11 = r, M21 = ±sin(phi) and
/// phi = Progress · MaxFoldDegrees. Destination pixels whose inverse lands
/// outside the source quad (sx/sy outside [-1,1]×[0,1]) are transparent, so
/// the desktop shows through around the fold. The DestY/SrcY remaps move the
/// hinge frame between the bottom and top screen edge (v runs 0=top row of
/// the image, D3D convention).
///
/// Pure data, no state, no D3D types: progress in, mapping out - the 0→1 and
/// 1→0 sweeps are the identical pure function (M1.4's bidirectional acceptance
/// is decided by the input signal, not by the mapping).
/// </summary>
public sealed record WarpFrame(
    double Progress,
    double M00,
    double M11,
    double M21,
    double DestYScale,
    double DestYOffset,
    double SrcYScale,
    double SrcYOffset,
    double EdgeFeather)
{
    /// <summary>Projected height of the folded quad (far edge height) in
    /// destination frame units: 1 at progress 0, decreasing toward 0 at the
    /// full fold. Tilt-toward can exceed 1 transiently? No - it shrinks too;
    /// only tilt-toward WIDTH grows past the frame.</summary>
    public double FarEdgeY => M00 / (M11 - M21);

    /// <summary>Projected half width of the folded quad's far edge, in
    /// destination frame units: 1 at progress 0; &lt; 1 for tilt-away,
    /// &gt; 1 (clipped by the viewport) for tilt-toward.</summary>
    public double FarEdgeHalfWidth => M11 / (M11 - M21);

    /// <summary>
    /// Inverse map: destination hinge-frame point → source hinge-frame point.
    /// Returns the raw (sx, sy) WITHOUT the inside-quad decision - callers
    /// apply their own boundary policy (the shader masks, the tests assert).
    /// </summary>
    public (double Sx, double Sy) InverseMap(double x, double y)
    {
        double w = M00 + M21 * y;
        return (M00 * x / w, M11 * y / w);
    }

    /// <summary>
    /// Forward map: source hinge-frame point → destination hinge-frame point
    /// (the projected position of the plane point). Exists so tests can
    /// verify the inverse map and compute the expected grid deformation for
    /// the real-machine visual check; the shader never uses it.
    /// </summary>
    public (double X, double Y) ForwardMap(double sx, double sy)
    {
        double depth = M11 - M21 * sy; // r − σ·s·sy: the plane's depth at that height
        return (M11 * sx / depth, M00 * sy / depth);
    }
}

/// <summary>Builds the per-frame warp mapping from a (smoothed) progress.</summary>
public static class WarpGeometry
{
    /// <summary>
    /// Computes the warp for one progress value. Pure function: the same
    /// progress always yields the same mapping, forward and reverse sweeps
    /// included. Progress is clamped into [0, 1]; anything that would make
    /// the mapping ill-defined (NaN, degenerate camera) throws - the render
    /// side has no meaningful fallback and must not render garbage silently.
    /// </summary>
    public static WarpFrame Compute(double progress, WarpOptions? options = null)
    {
        options ??= new WarpOptions();

        if (double.IsNaN(progress) || double.IsInfinity(progress))
        {
            throw new ArgumentOutOfRangeException(nameof(progress), progress,
                "Progress must be a finite number (the AnimationEngine never emits NaN/∞).");
        }
        double p = Math.Clamp(progress, 0.0, 1.0);

        if (options.MaxFoldDegrees <= 0.0 || options.MaxFoldDegrees > 90.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options),
                options.MaxFoldDegrees,
                "MaxFoldDegrees must be in (0, 90]: past 90° the plane folds past edge-on.");
        }
        double r = options.CameraDistanceRatio;
        if (double.IsNaN(r) || double.IsInfinity(r) || r <= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), r,
                "CameraDistanceRatio must be > 1.0 (r = 1 puts the far edge on the camera plane at full fold).");
        }
        double feather = options.EdgeFeather;
        if (double.IsNaN(feather) || feather < 0.0 || feather > 0.25)
        {
            throw new ArgumentOutOfRangeException(nameof(options), feather,
                "EdgeFeather must be in [0, 0.25] normalized frame units.");
        }

        double phi = p * options.MaxFoldDegrees * Math.PI / 180.0;
        double c = Math.Cos(phi);
        double s = Math.Sin(phi);
        double m21 = options.TiltAwayFromViewer ? -s : s;

        // Hinge at the bottom edge: v=1 (bottom row) is the hinge (y=0),
        // v=0 (top row) is the far edge. Hinge at the top: mirrored.
        double destScale = options.HingeAtTop ? 1.0 : -1.0;
        double destOffset = options.HingeAtTop ? 0.0 : 1.0;
        double srcScale = options.HingeAtTop ? 1.0 : -1.0;
        double srcOffset = options.HingeAtTop ? 0.0 : 1.0;

        return new WarpFrame(
            Progress: p,
            M00: r * c,
            M11: r,
            M21: m21,
            DestYScale: destScale,
            DestYOffset: destOffset,
            SrcYScale: srcScale,
            SrcYOffset: srcOffset,
            EdgeFeather: feather);
    }
}
