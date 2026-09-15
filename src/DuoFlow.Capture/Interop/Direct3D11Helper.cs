using System;
using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using Vortice.DXGI;
using Vortice.Direct3D11;

namespace DuoFlow.Capture.Interop;

/// <summary>
/// Conversions between WinRT (IDirect3DDevice / IDirect3DSurface) and native
/// D3D11 (IDXGIDevice / ID3D11Texture2D) objects.
/// Pattern from the official Microsoft GraphicsCapture samples.
/// Keeps the whole pipeline GPU -> GPU (no CPU staging anywhere).
/// </summary>
public static class Direct3D11Helper
{
    // ID3D11Texture2D
    private static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    [DllImport("d3d11.dll",
        EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice",
        SetLastError = true,
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        CallingConvention = CallingConvention.StdCall)]
    private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    /// <summary>
    /// WinRT IDirect3DDxgiInterfaceAccess: lets us retrieve the native DXGI/D3D
    /// pointer behind an IDirect3DSurface.
    /// </summary>
    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    public interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] ref Guid iid);
    }

    /// <summary>
    /// Wraps a native DXGI device into a WinRT IDirect3DDevice that
    /// Direct3D11CaptureFramePool accepts.
    /// </summary>
    public static IDirect3DDevice CreateIDirect3DDevice(IDXGIDevice dxgiDevice)
    {
        uint hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr pUnknown);
        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR(unchecked((int)hr));
        }

        IDirect3DDevice device = MarshalInterface<IDirect3DDevice>.FromAbi(pUnknown);
        Marshal.Release(pUnknown);
        return device;
    }

    /// <summary>
    /// Extracts the native ID3D11Texture2D behind a captured IDirect3DSurface.
    /// The returned Vortice object owns the reference (Dispose it when done).
    /// </summary>
    public static ID3D11Texture2D GetD3DTexture(IDirect3DSurface surface)
    {
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        Guid iid = IID_ID3D11Texture2D;
        IntPtr pTexture = access.GetInterface(ref iid);
        return new ID3D11Texture2D(pTexture); // ownership transferred to Vortice wrapper
    }
}
