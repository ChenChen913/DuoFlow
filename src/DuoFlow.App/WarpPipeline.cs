using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using DuoFlow.Render;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace DuoFlow.App;

/// <summary>
/// M1.4 Pass-1 warp pipeline (DD-039): draws one captured desktop texture
/// through the perspective warp (shaders/DuoWarp.hlsl) into the composition
/// swapchain back buffer.
///
/// Design points:
///  - Runs on the CAPTURE device (the desktop texture lives there; staying
///    on one device keeps the hybrid dual-GPU case simple - M1.4 introduces
///    no cross-adapter copies).
///  - The shader is compiled at startup with D3DCompile (Vortice.D3DCompiler)
///    from the .hlsl shipped next to the exe: no build-time FXC dependency,
///    so CI needs no extra workload.
///  - Outside the folded quad the shader emits premultiplied (0,0,0,0) and
///    the blend state composites over whatever is behind - the desktop stays
///    visible (P0-1 recipe untouched; this sits INSIDE the preview panel).
///  - TryCreate never throws: on any failure the renderer falls back to the
///    M0.2 straight blit so the CI smoke (WARP device) keeps producing frames.
/// </summary>
public sealed class WarpPipeline : IDisposable
{
    /// <summary>Constant buffer layout - must match the cbuffer in DuoWarp.hlsl
    /// (9 × float4 = 144 bytes; three float4s instead of float3x3 leave no
    /// row/column-major packing doubt).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Constants
    {
        public Vector4 Row0;
        public Vector4 Row1;
        public Vector4 Row2;
        public Vector4 DestYRemap;
        public Vector4 SrcYRemap;
        public Vector4 EdgeParams;
        public Vector4 MaskParams; // M1.5: x=center, y=width, z=falloff, w=debug
        public Vector4 BlurParams; // M1.6: x=maxBlurNorm, y=progress, z=aspect, w=-
        public Vector4 DimParams;  // M1.7: x=maxDarkness
    }

    public const string ShaderFileName = "DuoWarp.hlsl";

    private readonly ID3D11Device _device;
    private readonly int _width;
    private readonly int _height;

    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11Buffer? _constantBuffer;
    private ID3D11SamplerState? _sampler;
    private ID3D11RasterizerState? _rasterizer;
    private ID3D11RenderTargetView? _renderTarget;
    private bool _firstFrameTraced;
    private bool _disposed;

    private WarpPipeline(ID3D11Device device, int width, int height)
    {
        _device = device;
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Compiles the shader and creates the pipeline for the given back
    /// buffer. Returns null (with the failure traced) when ANYTHING fails -
    /// the caller keeps rendering with the M0.2 straight blit.
    /// </summary>
    public static WarpPipeline? TryCreate(ID3D11Device device, ID3D11Texture2D backBuffer)
    {
        WarpPipeline? pipeline = null;
        try
        {
            string shaderPath = Path.Combine(AppContext.BaseDirectory, "shaders", ShaderFileName);
            if (!File.Exists(shaderPath))
            {
                Trace.Log($"warp: {ShaderFileName} not found at {shaderPath} - staying on the M0.2 blit");
                return null;
            }

            Texture2DDescription bbDesc = backBuffer.Description;
            pipeline = new WarpPipeline(device, (int)bbDesc.Width, (int)bbDesc.Height);

            using Blob vsBlob = Compile(shaderPath, "VSMain", "vs_5_0");
            using Blob psBlob = Compile(shaderPath, "PSMain", "ps_5_0");
            pipeline._vertexShader = device.CreateVertexShader(vsBlob, null);
            pipeline._pixelShader = device.CreatePixelShader(psBlob, null);

            pipeline._constantBuffer = device.CreateBuffer(
                (uint)Marshal.SizeOf<Constants>(),
                BindFlags.ConstantBuffer,
                ResourceUsage.Default,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0);

            pipeline._sampler = device.CreateSamplerState(new SamplerDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                MipLODBias = 0f,
                MaxAnisotropy = 0,
                ComparisonFunc = ComparisonFunction.Never,
                BorderColor = default,
                MinLOD = 0f,
                MaxLOD = float.MaxValue,
            });

            // No blend state needed: every frame clears the whole back buffer
            // and draws exactly one fullscreen pass, so the shader's
            // premultiplied output lands in the buffer untouched; DWM then
            // composites the swapchain over the desktop by its alpha channel
            // (AlphaMode.Premultiplied). Blending would be a no-op here.

            pipeline._rasterizer = device.CreateRasterizerState(new RasterizerDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None, // fullscreen triangle winding is irrelevant
                FrontCounterClockwise = false,
                DepthBias = 0,
                DepthBiasClamp = 0f,
                SlopeScaledDepthBias = 0f,
                DepthClipEnable = false,
                ScissorEnable = false,
                MultisampleEnable = false,
                AntialiasedLineEnable = false,
            });

            pipeline._renderTarget = device.CreateRenderTargetView(backBuffer, null);

            Trace.Log($"warp: pipeline ready ({pipeline._width}×{pipeline._height}, {ShaderFileName})");
            return pipeline;
        }
        catch (Exception ex)
        {
            Trace.Log($"warp: init FAILED: {ex.GetType().Name}: {ex.Message} - falling back to the M0.2 blit");
            try { pipeline?.Dispose(); } catch { /* best effort */ }
            return null;
        }
    }

    private static Blob Compile(string shaderPath, string entryPoint, string profile)
    {
        Result result = Compiler.CompileFromFile(
            shaderPath, null, null, entryPoint, profile,
            ShaderFlags.None, EffectFlags.None,
            out Blob? bytecode, out Blob? errors);
        if (result.Failure || bytecode is null)
        {
            string details = "";
            try
            {
                if (errors is not null)
                {
                    details = Marshal.PtrToStringAnsi(
                        errors.BufferPointer, (int)(ulong)errors.BufferSize) ?? "";
                }
            }
            catch { /* keep the raw result */ }
            throw new InvalidOperationException(
                $"{ShaderFileName}:{entryPoint} ({profile}) compile failed: {result} {details}".TrimEnd());
        }
        errors?.Dispose();
        return bytecode;
    }

    /// <summary>
    /// One warp pass: clear the back buffer to transparent, tick the constant
    /// buffer with the frame's homography + the hinge mask parameters, sample
    /// the captured desktop through the warp, draw. The SRV is unbound before
    /// returning - the captured texture is released by the frame pool right
    /// after this call.
    /// </summary>
    public void Render(ID3D11DeviceContext context, ID3D11Texture2D desktopTexture, WarpFrame frame,
        HingeMaskProfile mask, bool debugMask, double maxBlurNormalized, double progress,
        double maxDarkness)
    {
        if (_disposed || _renderTarget is null)
        {
            return;
        }

        var constants = new Constants
        {
            Row0 = new Vector4((float)frame.M00, 0f, 0f, 0f),
            Row1 = new Vector4(0f, (float)frame.M11, 0f, 0f),
            Row2 = new Vector4(0f, (float)frame.M21, (float)frame.M00, 0f),
            DestYRemap = new Vector4((float)frame.DestYScale, (float)frame.DestYOffset, 0f, 0f),
            SrcYRemap = new Vector4((float)frame.SrcYScale, (float)frame.SrcYOffset, 0f, 0f),
            EdgeParams = new Vector4((float)frame.EdgeFeather, 0f, 0f, 0f),
            // M1.5 hinge mask (DD-040): evaluated in the warp's hinge frame;
            // w = debug flag switches the shader to the mask heat ramp.
            MaskParams = new Vector4(
                (float)mask.HingeCenter,
                (float)mask.HingeWidth,
                (float)mask.FalloffExponent,
                debugMask ? 1f : 0f),
            // M1.6 blur (DD-041): radius = mask × progress × maxBlur, applied
            // in the source frame; z carries the frame aspect for round taps.
            BlurParams = new Vector4(
                (float)maxBlurNormalized,
                (float)progress,
                (float)((double)_height / _width),
                0f),
            // M1.7 dimming (DD-042): brightness = 1 - mask × progress × x.
            DimParams = new Vector4((float)maxDarkness, 0f, 0f, 0f),
        };
        context.UpdateSubresource(constants, _constantBuffer!, 0, 0, 0, null);

        context.ClearRenderTargetView(_renderTarget, new Color4(0f, 0f, 0f, 0f));
        context.RSSetViewports(new[] { new Viewport(0, 0, _width, _height) });
        context.RSSetState(_rasterizer);
        context.OMSetRenderTargets(_renderTarget, null);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(_vertexShader, (ID3D11ClassInstance[]?)null, 0);
        context.PSSetShader(_pixelShader, (ID3D11ClassInstance[]?)null, 0);
        context.PSSetConstantBuffers(0, new[] { _constantBuffer! });
        context.PSSetSamplers(0, new[] { _sampler! });

        using ID3D11ShaderResourceView srv = _device.CreateShaderResourceView(desktopTexture, null);
        context.PSSetShaderResources(0, new[] { srv });
        context.Draw(3, 0);

        // The WGC frame texture is disposed by DesktopCapture the moment this
        // callback returns - no dangling references may remain.
        context.PSSetShaderResources(0, new ID3D11ShaderResourceView[] { null! });

        // One-time first-frame readback: proves whether the draw produced
        // opaque pixels (warp active) or left the buffer transparent, so a
        // real-machine failure is diagnosable from duoflow-trace.txt alone.
        if (!_firstFrameTraced)
        {
            _firstFrameTraced = true;
            try
            {
                var stagingDesc = new Texture2DDescription
                {
                    Width = (uint)_width,
                    Height = (uint)_height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CPUAccessFlags = CpuAccessFlags.Read,
                    MiscFlags = ResourceOptionFlags.None,
                };
                using ID3D11Texture2D staging = _device.CreateTexture2D(stagingDesc);
                context.CopyResource(staging, _renderTarget.Resource);
                MappedSubresource map = context.Map(staging, 0, MapMode.Read);
                try
                {
                    string Probe(int px, int py)
                    {
                        IntPtr basePtr = map.DataPointer + (int)(py * (long)map.RowPitch) + px * 4;
                        byte b = Marshal.ReadByte(basePtr);
                        byte g = Marshal.ReadByte(basePtr, 1);
                        byte r = Marshal.ReadByte(basePtr, 2);
                        byte a = Marshal.ReadByte(basePtr, 3);
                        return $"({r},{g},{b},{a})";
                    }
                    Trace.Log($"warp first frame: progress={frame.Progress:0.000} M00={frame.M00:0.000} M21={frame.M21:0.000} " +
                              $"center={Probe(_width / 2, _height / 2)} quarter={Probe(_width / 4, _height / 4)} " +
                              $"topleft={Probe(1, 1)} bottomright={Probe(_width - 2, _height - 2)}");
                }
                finally
                {
                    context.Unmap(staging, 0);
                }
            }
            catch (Exception ex)
            {
                Trace.Log($"warp first-frame readback FAILED: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        try { _renderTarget?.Dispose(); } catch { }
        try { _rasterizer?.Dispose(); } catch { }
        try { _sampler?.Dispose(); } catch { }
        try { _constantBuffer?.Dispose(); } catch { }
        try { _pixelShader?.Dispose(); } catch { }
        try { _vertexShader?.Dispose(); } catch { }
        _renderTarget = null;
        _rasterizer = null;
        _sampler = null;
        _constantBuffer = null;
        _pixelShader = null;
        _vertexShader = null;
    }
}
