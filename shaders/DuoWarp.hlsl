// =====================================================================
// DuoFlow - fold composite (M1.8, model v2 - DD-046)
//
// Physical depth-of-field model, adopted from the DuoFold-Android reference
// (studied 2026-09-19, replaces the mask+blur+dim trio of DD-040..DD-042):
// the desktop is a plane, the screen is frosted glass tilting above it
// around a hinge. Every pixel's GLASS-to-DESKTOP GAP drives everything:
//
//   normGap = y_dest * sin(phi)              (0 at the hinge, 1 at full fold)
//   ease    = normGap^2                      (quadratic ease-in: hinge stays
//                                             sharp, far edge melts)
//   coc     = saturate(ease * 1.5 * intensity)   (circle of confusion, 0..1)
//   radius  = coc * MaxBlurPixels            (frosted blur, device pixels)
//   light   = saturate(1 - 0.006 * radius)   (tilted surface catches less
//                                             light -> the shadow transition)
//
// The fold geometry itself is the same inverse homography as before
// (DD-039): (sx, sy) = (M00·x, M11·y) / (M00 + M21·y) - at progress 0 it is
// the identity. Outside the projected quad: premultiplied transparent.
//
// Blur quality (the velvety frosted look, per the reference):
//   - the source texture carries a full MIP chain; taps sample
//     SampleLevel with lod = coc*3 so large radii arrive pre-filtered
//     (no mosaic, no glow);
//   - 16-tap Vogel spiral, EQUAL weights, NO center tap - high-contrast
//     edges and text fully dissolve into the frost;
//   - Interleaved Gradient Noise per-pixel rotation hides the spiral;
//   - out-of-bounds source reads BLACK (the desktop's infinite black
//     border) with a 1-texel soft edge.
//
// Progress enters ONLY through the matrix (phi) and the fade opacity: at
// progress 0 the matrix is the identity and FadeParams.x = 0, so a fully
// open desktop shows nothing at all.
// =====================================================================

cbuffer FoldConstants : register(b0)
{
    // Homography rows: [[M00, 0, 0], [0, M11, 0], [0, M21, M00]].
    float4 Row0;   // (M00, 0,   0,   -)
    float4 Row1;   // (0,   M11, 0,   -)
    float4 Row2;   // (0,   M21, M00, -)

    // y_dest = DestYRemap.x·v_dest + DestYRemap.y  (0 at the hinge, 1 far edge)
    float4 DestYRemap;
    // v_src  = SrcYRemap.x·sy + SrcYRemap.y        (D3D: v=0 is the top row)
    float4 SrcYRemap;
    // x: feather width in normalized units; yzw reserved.
    float4 EdgeParams;
    // M1.8 model v2 physics:
    // x: max blur radius (device pixels, 160)
    // y: effect intensity (the envelope-applied 0..1)
    // z: 1 / render width
    // w: 1 / render height
    float4 PhysicsParams;
    // x: global fade opacity [0,1] (0 while fully open)
    float4 FadeParams;
};

Texture2D    DesktopTexture : register(t0);
SamplerState LinearClamp   : register(s0);

struct VSOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

VSOutput VSMain(uint vertexId : SV_VertexID)
{
    // Fullscreen triangle - no vertex buffer, no input layout.
    VSOutput output;
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    output.Position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    output.TexCoord = uv;
    return output;
}

// Out-of-bounds reads BLACK (the desktop's infinite black border), with a
// 1-texel soft edge so the frame line never aliases.
float3 SampleDesktop(float2 uv, float lod)
{
    if (any(uv < 0.0) || any(uv > 1.0))
    {
        return float3(0.0, 0.0, 0.0);
    }
    float2 edgeTexels = min(uv, 1.0 - uv) / PhysicsParams.zw;
    float edgeAlpha = saturate(min(edgeTexels.x, edgeTexels.y));
    return DesktopTexture.SampleLevel(LinearClamp, uv, lod).rgb * edgeAlpha;
}

float4 PSMain(VSOutput input) : SV_Target
{
    float x = input.TexCoord.x * 2.0 - 1.0;                              // [-1, 1]
    float y = saturate(DestYRemap.x * input.TexCoord.y + DestYRemap.y);  // [0, 1]

    float w = Row2.y * y + Row2.z;          // M21·y + M00
    if (w < 1e-6)
    {
        return float4(0.0, 0.0, 0.0, 0.0);  // degenerate fold region
    }

    float sx = Row0.x * x / w;              // M00·x / w
    float sy = Row1.y * y / w;              // M11·y / w

    // Outside the projected quad the real desktop must show through.
    bool inside = sy >= 0.0 && sy <= 1.0 && sx >= -1.0 && sx <= 1.0;
    if (!inside)
    {
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    // Feathered quad boundary (premultiplied alpha).
    float f = max(EdgeParams.x, 1e-5);
    float a = smoothstep(0.0, f, sy) * smoothstep(0.0, f, 1.0 - sy)
            * smoothstep(0.0, f, sx + 1.0) * smoothstep(0.0, f, 1.0 - sx);

    // ---- Physical depth-of-field (DD-046) ----
    // Gap height of the folded glass AT THE DISPLAYED SURFACE POINT: the
    // inverse-mapped hinge-frame height sy (how far the source point sits
    // from the hinge) times the tilt's sine. Source-side, not dest-side:
    // the circle of confusion belongs to the object point - the dest-based
    // variant under-blurs the far content ~4x at mid fold.
    float normGap = saturate(sy * abs(Row2.y));
    float ease = normGap * normGap;                      // quadratic ease-in
    float coc = saturate(ease * 1.5 * PhysicsParams.y);  // circle of confusion
    float radiusPx = coc * PhysicsParams.x;              // frosted radius, px
    float lod = saturate(coc * 3.0);                     // mip pre-filter depth
    float light = saturate(1.0 - 0.006 * radiusPx);      // light falloff

    float2 sourceUv = float2(sx * 0.5 + 0.5, SrcYRemap.x * sy + SrcYRemap.y);
    float3 rgb;
    if (coc < 0.001)
    {
        rgb = SampleDesktop(sourceUv, 0.0);              // sharp (hinge zone)
    }
    else
    {
        // 16-tap Vogel spiral, equal weights, no center tap, IGN rotation.
        float ign = frac(52.9829189 * frac(dot(input.Position.xy,
            float2(0.06711056, 0.00583715))));
        float2 dir = float2(cos(ign * 6.2831853), sin(ign * 6.2831853));
        const float C_STEP = -0.73736888;   // cos(golden angle)
        const float S_STEP = 0.67549029;    // sin(golden angle)
        float2 stepUv = radiusPx * PhysicsParams.zw;

        float3 acc = float3(0.0, 0.0, 0.0);
        [unroll]
        for (int i = 0; i < 16; i++)
        {
            float r = sqrt((float(i) + 0.5) * 0.0625);
            acc += SampleDesktop(sourceUv + dir * (r * stepUv), lod);
            dir = float2(dir.x * C_STEP - dir.y * S_STEP,
                         dir.y * C_STEP + dir.x * S_STEP);
        }
        rgb = acc / 16.0;
    }

    // The shadow transition: light falls as the frost deepens.
    rgb *= light;

    return float4(rgb * a * FadeParams.x, a * FadeParams.x);
}
