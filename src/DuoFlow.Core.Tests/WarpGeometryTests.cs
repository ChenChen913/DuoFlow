using System;
using System.Collections.Generic;
using DuoFlow.Render;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M1.4 Pass-1 warp geometry (DD-039). The fold must be a true projective
/// mapping: identity at progress 0, strictly monotonic in progress, finite
/// across the whole range, reversible (0→1 and 1→0 are the same pure
/// function), and projective rather than a uniform scale (the far edge
/// width/height ratio follows 1/cos(phi), which no scale impostor matches).
/// </summary>
public class WarpGeometryTests
{
    private const double Eps = 1e-9;

    // ---------------------------------------------------------------
    // Identity at progress 0
    // ---------------------------------------------------------------

    [Fact]
    public void Compute_ProgressZero_IsIdentity()
    {
        WarpFrame f = WarpGeometry.Compute(0.0);

        Assert.Equal(f.M00, f.M11, 12);          // diag(r, r)
        Assert.Equal(0.0, f.M21, 12);            // no shear at all
        Assert.Equal(f.M00, f.M11 - f.M21, 12);  // far edge denominator = r

        foreach ((double x, double y) in new[] { (-1.0, 0.0), (0.0, 0.0), (0.3, 0.4), (-0.7, 0.9), (1.0, 1.0) })
        {
            (double sx, double sy) = f.InverseMap(x, y);
            Assert.Equal(x, sx, 12);
            Assert.Equal(y, sy, 12);
        }
    }

    [Fact]
    public void Compute_ProgressZero_QuadFillsFrame()
    {
        WarpFrame f = WarpGeometry.Compute(0.0);
        Assert.Equal(1.0, f.FarEdgeY, 12);
        Assert.Equal(1.0, f.FarEdgeHalfWidth, 12);
    }

    // ---------------------------------------------------------------
    // Monotonic folding (tilt-away: quad narrows toward the far edge)
    // ---------------------------------------------------------------

    [Fact]
    public void FarEdge_TiltAway_ShrinksMonotonicallyWithProgress()
    {
        double lastHeight = double.MaxValue;
        double lastWidth = double.MaxValue;
        for (int i = 0; i <= 100; i++)
        {
            WarpFrame f = WarpGeometry.Compute(i / 100.0);
            Assert.True(f.FarEdgeY < lastHeight, $"FarEdgeY not decreasing at p={i / 100.0}");
            Assert.True(f.FarEdgeHalfWidth < lastWidth, $"FarEdgeHalfWidth not decreasing at p={i / 100.0}");
            Assert.InRange(f.FarEdgeY, 0.0, 1.0);
            Assert.InRange(f.FarEdgeHalfWidth, 0.0, 1.0);
            lastHeight = f.FarEdgeY;
            lastWidth = f.FarEdgeHalfWidth;
        }
        // Full fold (90°): the plane is edge-on - zero height remains.
        Assert.Equal(0.0, WarpGeometry.Compute(1.0).FarEdgeY, 12);
    }

    [Fact]
    public void FarEdge_TiltToward_WidensPastFrame()
    {
        WarpOptions opts = new() { TiltAwayFromViewer = false };
        for (int i = 1; i <= 10; i++)
        {
            WarpFrame f = WarpGeometry.Compute(i / 10.0, opts);
            Assert.True(f.FarEdgeHalfWidth > 1.0, $"tilt-toward far edge must exceed the frame at p={i / 10.0}");
            // Height compresses overall but may transiently exceed 1: the far
            // edge comes TOWARD the camera, so it projects higher before the
            // cos(phi) flattening wins. Only monotone-to-zero at full fold.
            Assert.True(f.FarEdgeY > 0.0 && double.IsFinite(f.FarEdgeY));
            Assert.True(f.M11 - f.M21 > 0.0);     // camera never crossed (r > 1 guaranteed)
        }
        Assert.Equal(0.0, WarpGeometry.Compute(1.0, opts).FarEdgeY, 12);
    }

    // ---------------------------------------------------------------
    // Quad corners round-trip (the inverse map is the real shader input)
    // ---------------------------------------------------------------

    [Fact]
    public void InverseMap_QuadCorners_MapToSourceCorners()
    {
        WarpFrame f = WarpGeometry.Compute(0.5);

        // Hinge edge is pinned to the bottom of the frame.
        (double sx, double sy) = f.InverseMap(-1.0, 0.0);
        Assert.Equal(-1.0, sx, 9);
        Assert.Equal(0.0, sy, 9);

        (sx, sy) = f.InverseMap(0.0, 0.0);
        Assert.Equal(0.0, sx, 9);
        Assert.Equal(0.0, sy, 9);

        (sx, sy) = f.InverseMap(1.0, 0.0);
        Assert.Equal(1.0, sx, 9);
        Assert.Equal(0.0, sy, 9);

        // Far edge maps to the top of the source quad.
        (sx, sy) = f.InverseMap(f.FarEdgeHalfWidth, f.FarEdgeY);
        Assert.Equal(1.0, sx, 9);
        Assert.Equal(1.0, sy, 9);

        (sx, sy) = f.InverseMap(-f.FarEdgeHalfWidth, f.FarEdgeY);
        Assert.Equal(-1.0, sx, 9);
        Assert.Equal(1.0, sy, 9);

        // Above the far edge there is no source content (the test consumer
        // would mask it) - the inverse still lands above sy=1, never NaN.
        (sx, sy) = f.InverseMap(0.0, 1.0);
        Assert.True(double.IsFinite(sx) && double.IsFinite(sy));
        Assert.True(sy > 1.0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(0.8)]
    [InlineData(1.0)]
    public void Forward_Inverse_RoundTripsEverywhereInside(double progress)
    {
        WarpFrame f = WarpGeometry.Compute(progress);
        for (int i = 0; i <= 10; i++)
        {
            for (int j = 0; j <= 10; j++)
            {
                double sx = -1.0 + 0.2 * i;
                double sy = 0.1 * j;
                (double x, double y) = f.ForwardMap(sx, sy);
                (double sx2, double sy2) = f.InverseMap(x, y);
                Assert.Equal(sx, sx2, 9);
                Assert.Equal(sy, sy2, 9);
            }
        }
    }

    // ---------------------------------------------------------------
    // Projective signature (catches a scale impostor)
    // ---------------------------------------------------------------

    [Fact]
    public void FarEdgeRatio_FollowsProjectiveModel_NotAScale()
    {
        // Width/height ratio of the far edge must equal 1/cos(phi): the fold
        // compresses height faster than width. A uniform-scale impostor would
        // keep the quad's aspect constant (ratio == 1). p < 1 only: at the
        // full fold the height reaches 0 and the ratio degenerates (0/0-like).
        for (int i = 1; i <= 9; i++)
        {
            double p = i / 10.0;
            WarpFrame f = WarpGeometry.Compute(p);
            double phi = p * Math.PI / 2.0;
            double expected = 1.0 / Math.Cos(phi);
            double actual = f.FarEdgeHalfWidth / f.FarEdgeY;
            Assert.True(Math.Abs(actual - expected) < 1e-9 * expected,
                $"far-edge ratio at p={p}: expected {expected}, got {actual}");
            Assert.True(actual > 1.0, "fold must compress height faster than width (projective)");
        }

        // Camera distance must matter (perspective strength is a real knob).
        double narrow = WarpGeometry.Compute(0.5, new WarpOptions { CameraDistanceRatio = 1.5 }).FarEdgeHalfWidth;
        double wide = WarpGeometry.Compute(0.5, new WarpOptions { CameraDistanceRatio = 4.0 }).FarEdgeHalfWidth;
        Assert.True(narrow < 0.9 && wide > narrow, $"r must control perspective strength (got {narrow} vs {wide})");
    }

    // ---------------------------------------------------------------
    // Continuity + bidirectional sweep (M1.4 acceptance: 0→1 and 1→0)
    // ---------------------------------------------------------------

    [Fact]
    public void Sweep_UpThenDown_IsContinuousAndHysteresisFree()
    {
        const int steps = 200;
        var up = new List<WarpFrame>(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            up.Add(WarpGeometry.Compute(i / (double)steps));
        }
        var down = new List<WarpFrame>(steps + 1);
        for (int i = steps; i >= 0; i--)
        {
            down.Add(WarpGeometry.Compute(i / (double)steps));
        }

        // Same progress => identical mapping in both directions (no hidden state).
        for (int i = 0; i <= steps; i++)
        {
            Assert.Equal(up[i].FarEdgeY, down[steps - i].FarEdgeY, 15);
            Assert.Equal(up[i].FarEdgeHalfWidth, down[steps - i].FarEdgeHalfWidth, 15);
            Assert.Equal(up[i].M00, down[steps - i].M00, 15);
        }

        // Continuity: no step anywhere may move the quad edge by more than
        // 1% of the frame (the animation engine slews on top of this).
        for (int i = 1; i <= steps; i++)
        {
            double dh = Math.Abs(up[i].FarEdgeY - up[i - 1].FarEdgeY);
            double dw = Math.Abs(up[i].FarEdgeHalfWidth - up[i - 1].FarEdgeHalfWidth);
            Assert.True(dh < 0.01, $"FarEdgeY jumps at p={i / (double)steps} (Δ={dh})");
            Assert.True(dw < 0.01, $"FarEdgeHalfWidth jumps at p={i / (double)steps} (Δ={dw})");
            Assert.True(double.IsFinite(up[i].FarEdgeY) && double.IsFinite(up[i].FarEdgeHalfWidth));
        }
    }

    // ---------------------------------------------------------------
    // Hinge at the top mirrors the bottom-hinge mapping
    // ---------------------------------------------------------------

    [Fact]
    public void HingeAtTop_MirrorsHingeAtBottom()
    {
        WarpFrame bottom = WarpGeometry.Compute(0.6);
        WarpFrame top = WarpGeometry.Compute(0.6, new WarpOptions { HingeAtTop = true });

        // Remap constants: bottom hinge sits at v=1 (y = 1 - v), top hinge at
        // v=0 (y = v); source rows mirror the same way.
        Assert.Equal(-1.0, bottom.DestYScale, 12);
        Assert.Equal(1.0, bottom.DestYOffset, 12);
        Assert.Equal(1.0, top.DestYScale, 12);
        Assert.Equal(0.0, top.DestYOffset, 12);
        Assert.Equal(-1.0, bottom.SrcYScale, 12);
        Assert.Equal(1.0, bottom.SrcYOffset, 12);
        Assert.Equal(1.0, top.SrcYScale, 12);
        Assert.Equal(0.0, top.SrcYOffset, 12);

        // The top-hinge fold is the bottom-hinge fold with the frame flipped
        // vertically: mapping (x, v) on top == mapping (x, 1-v) on bottom,
        // with the resulting source row mirrored (v_src -> 1 - v_src).
        double x = 0.3, v = 0.2;
        double yTop = top.DestYScale * v + top.DestYOffset;
        double yBottomFlipped = bottom.DestYScale * (1.0 - v) + bottom.DestYOffset;
        Assert.Equal(yTop, yBottomFlipped, 12); // same hinge-frame height

        (double sxT, double syT) = top.InverseMap(x, yTop);
        (double sxB, double syB) = bottom.InverseMap(x, yBottomFlipped);
        Assert.Equal(sxT, sxB, 12);
        Assert.Equal(syT, syB, 12);

        double vSrcTop = top.SrcYScale * syT + top.SrcYOffset;
        double vSrcBottom = bottom.SrcYScale * syB + bottom.SrcYOffset;
        Assert.Equal(vSrcTop, 1.0 - vSrcBottom, 12);
    }

    // ---------------------------------------------------------------
    // Input validation
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Compute_NonFiniteProgress_Throws(double progress)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WarpGeometry.Compute(progress));
    }

    [Fact]
    public void Compute_OutOfRangeProgress_Clamps()
    {
        WarpFrame zero = WarpGeometry.Compute(0.0);
        WarpFrame one = WarpGeometry.Compute(1.0);

        Assert.Equal(zero, WarpGeometry.Compute(-0.7));
        Assert.Equal(one, WarpGeometry.Compute(1.7));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(90.1)]
    [InlineData(-30.0)]
    public void Compute_BadMaxFold_Throws(double maxFold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WarpGeometry.Compute(0.5, new WarpOptions { MaxFoldDegrees = maxFold }));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(double.NaN)]
    public void Compute_BadCameraDistance_Throws(double r)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WarpGeometry.Compute(0.5, new WarpOptions { CameraDistanceRatio = r }));
    }

    [Fact]
    public void Compute_MaxFoldApplied_FeatherRoundTrips()
    {
        WarpFrame f = WarpGeometry.Compute(1.0, new WarpOptions
        {
            MaxFoldDegrees = 45.0,
            EdgeFeather = 0.01,
        });

        double phi = Math.PI / 4.0;
        Assert.Equal(2.5 * Math.Cos(phi) / (2.5 + Math.Sin(phi)), f.FarEdgeY, 9);
        Assert.Equal(2.5 / (2.5 + Math.Sin(phi)), f.FarEdgeHalfWidth, 9);
        Assert.Equal(0.01, f.EdgeFeather, 12);
        Assert.Equal(1.0, f.Progress, 12);
    }
}
