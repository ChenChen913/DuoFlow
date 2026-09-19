// =====================================================================
// DuoFlow - Pass 1: Perspective Warp (M1.4, DD-039)
//
// Models the desktop as a plane hinged along one screen edge (bottom by
// default) that rotates around that hinge as Progress goes 0 -> 1, viewed
// through a pinhole camera. The projected image is a true projective
// transform (a homography), NOT a scale: the far edge narrows (tilt-away)
// or widens (tilt-toward) nonlinearly while the whole image compresses
// toward the hinge - PROJECT_SPEC §10 / TECHNICAL_PROPOSAL §7.
//
// The pixel shader evaluates the INVERSE homography per pixel:
//
//     (sx, sy) = (M00·x, M11·y) / (M00 + M21·y)        (hinge frame)
//
// with (x, y) = destination position (x across [-1,1], y from the hinge
// [0,1]) and (sx, sy) = source position in the same frame. At progress 0
// the matrix is r·I, i.e. the identity - the panel shows the capture as-is
// (the M0.2 behavior). Destination pixels whose inverse lands outside the
// source quad emit PREMULTIPLIED transparent black (0,0,0,0) so the real
// desktop shows through around the fold (the composition swapchain is
// AlphaMode.Premultiplied).
//
// Coefficients are produced per frame by DuoFlow.Render.WarpGeometry
// (pure C#, unit tested) from the M1.3 AnimationEngine's smoothed Progress
// (DD-002: the shader knows nothing about providers; DD-038: the engine is
// the render side's only progress source).
// =====================================================================

cbuffer WarpConstants : register(b0)
{
    // Homography rows: [[M00, 0, 0], [0, M11, 0], [0, M21, M00]].
    // Three float4s instead of a float3x3: no row/column-major packing
    // ambiguity across toolchains, each register is a full row.
    float4 Row0;   // (M00, 0,   0,   -)
    float4 Row1;   // (0,   M11, 0,   -)
    float4 Row2;   // (0,   M21, M00, -)

    // y_dest = DestYRemap.x·v_dest + DestYRemap.y  (0 at the hinge, 1 far edge)
    float4 DestYRemap;
    // v_src  = SrcYRemap.x·sy + SrcYRemap.y        (D3D: v=0 is the top row)
    float4 SrcYRemap;
    // x: feather width in normalized units; yzw reserved.
    float4 EdgeParams;
    // M1.5 Hinge Mask (DD-040), evaluated in the SAME hinge frame as the
    // warp (DD-039: mask and warp share one hinge definition):
    //   d = |y - x|;  t = saturate(d / y);  m = (1 - smoothstep(0,1,t))^z
    // x: hinge center (normalized distance from the hinge edge)
    // y: hinge width (mask reaches 0 at this distance)
    // z: falloff exponent (shape of the falloff)
    // w: debug flag (>0.5 -> output the mask heat ramp instead of the image)
    float4 MaskParams;
    // M1.6 Blur (DD-041):
    // x: max blur radius, normalized to the SOURCE frame height
    //    (CPU: MaxBlurPixels / captureHeight)
    // y: progress (0..1) - the pass scales the mask by it
    // z: frame height over frame width (aspect correction for round kernels)
    // w: reserved
    float4 BlurParams;
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

float4 PSMain(VSOutput input) : SV_Target
{
    float x = input.TexCoord.x * 2.0 - 1.0;                              // [-1, 1]
    float y = saturate(DestYRemap.x * input.TexCoord.y + DestYRemap.y);  // [0, 1]

    float w = Row2.y * y + Row2.z;          // M21·y + M00
    if (w < 1e-6)
    {
        // Degenerate fold region (full fold / behind-camera side of the
        // tilt-toward case): no image exists there - transparent.
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    float sx = Row0.x * x / w;              // M00·x / w
    float sy = Row1.y * y / w;              // M11·y / w

    // Outside the projected quad the real desktop must show through.
    bool inside = sy >= 0.0 && sy <= 1.0 && sx >= -1.0 && sx <= 1.0;
    if (!inside)
    {
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    // Feathered quad boundary (premultiplied alpha) - hides the fold's
    // aliasing edge against the live desktop. At progress 0 the quad IS the
    // panel, so this only softens the panel's own border over identical
    // content (invisible).
    float f = max(EdgeParams.x, 1e-5);
    float a = smoothstep(0.0, f, sy) * smoothstep(0.0, f, 1.0 - sy)
            * smoothstep(0.0, f, sx + 1.0) * smoothstep(0.0, f, 1.0 - sx);

    // M1.5 Hinge Mask (DD-040): evaluated in the SAME hinge frame as the
    // warp (the y above), peak at MaskParams.x, zero at center ± width.
    float maskT = saturate(abs(y - MaskParams.x) / MaskParams.y);
    float maskS = maskT * maskT * (3.0 - 2.0 * maskT);              // smoothstep
    float mask = pow(saturate(1.0 - maskS), MaskParams.z);           // falloff

    if (MaskParams.w > 0.5)
    {
        // Debug Mask visualization: pure heat ramp, RED = hinge (mask 1),
        // BLUE = far (mask 0). The R channel decodes the mask exactly
        // (m = R/A on the premultiplied output). Alpha rules unchanged.
        return float4(float3(mask, 0.2, 1.0 - mask) * a, a);
    }

    float2 sourceUv = float2(sx * 0.5 + 0.5, SrcYRemap.x * sy + SrcYRemap.y);
    float3 rgb = DesktopTexture.Sample(LinearClamp, sourceUv).rgb;

    // M1.6 Blur (DD-041): radius = mask × progress × maxBlur, applied in the
    // SOURCE frame (offsets map linearly through SrcYRemap). 13-tap hexagonal
    // kernel; radius 0 collapses onto the center tap (progress 0 = no blur
    // anywhere, hinge rows only blur as the mask rises). The mask is taken at
    // the DEST pixel - PROJECT_SPEC §12 "blur where the fold is".
    float radius = mask * BlurParams.y * BlurParams.x;
    if (radius > 1e-5)
    {
        static const float2 kDirs[6] = {
            float2( 1.00,  0.00), float2( 0.50,  0.87), float2(-0.50,  0.87),
            float2(-1.00,  0.00), float2(-0.50, -0.87), float2( 0.50, -0.87)
        };
        // Aspect: sx units span half the frame width, sy units the height.
        float aspect = BlurParams.z;
        float3 acc = rgb * 0.25;
        [unroll]
        for (int k = 0; k < 6; k++)
        {
            float sxA = sx + kDirs[k].x * radius * 0.5 * aspect;
            float syA = sy + kDirs[k].y * radius * 0.5;
            float sxO = sx + kDirs[k].x * radius * aspect;
            float syO = sy + kDirs[k].y * radius;
            float2 uvA = float2(sxA * 0.5 + 0.5, SrcYRemap.x * syA + SrcYRemap.y);
            float2 uvO = float2(sxO * 0.5 + 0.5, SrcYRemap.x * syO + SrcYRemap.y);
            acc += DesktopTexture.Sample(LinearClamp, uvA).rgb * (0.5 / 6.0);
            acc += DesktopTexture.Sample(LinearClamp, uvO).rgb * (0.25 / 6.0);
        }
        rgb = acc;
    }

    return float4(rgb * a, a);
}
