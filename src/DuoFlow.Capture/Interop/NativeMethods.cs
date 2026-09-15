using System;
using System.Runtime.InteropServices;

namespace DuoFlow.Capture.Interop;

/// <summary>
/// Win32 / COM interop entry points used by the capture pipeline.
/// </summary>
public static class NativeMethods
{
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    /// <summary>HMONITOR of the primary display.</summary>
    public static IntPtr GetPrimaryMonitorHandle()
        => MonitorFromWindow(GetDesktopWindow(), MONITOR_DEFAULTTOPRIMARY);

    /// <summary>
    /// GraphicsCaptureItem activation-factory interop: lets us bind the capture
    /// to a specific monitor without showing the picker UI.
    /// </summary>
    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, ref Guid iid);

        IntPtr CreateForMonitor(IntPtr hmon, ref Guid iid);
    }

    // Windows.Graphics.Capture.GraphicsCaptureItem
    public static readonly Guid GraphicsCaptureItemIID = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
}
