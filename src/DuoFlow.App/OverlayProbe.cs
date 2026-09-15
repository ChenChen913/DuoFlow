using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DuoFlow.App;

/// <summary>
/// M0.3 verification matrix for the overlay window. Serialized to
/// duoflow-overlay-smoke.json and asserted by the CI gate.
/// </summary>
public sealed class OverlayReport
{
    public string Phase { get; set; } = "M0.3-overlay";

    public bool OverlayCreated { get; set; }
    public bool Fullscreen { get; set; }
    public bool Topmost { get; set; }
    public bool ClickThrough { get; set; }
    public bool NoActivate { get; set; }
    public bool ToolWindow { get; set; }
    public bool TransparentDwm { get; set; }

    public string WindowRect { get; set; } = "";
    public string ScreenRect { get; set; } = "";

    public int MonitorCount { get; set; }
    public string Monitors { get; set; } = "";

    public long CaptureFrames { get; set; }
    public double CaptureFps { get; set; }
    public string CaptureState { get; set; } = "unknown";

    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Collects the overlay window properties (styles are read back from the
/// actual Win32 window, not from our intentions) and persists the smoke
/// result for the CI acceptance gate.
///
/// Every section is fault-isolated: a projection glitch in one section must
/// not void the whole report (CI asserts on the booleans, warnings explain
/// what could not be read).
/// </summary>
public static class OverlayProbe
{
    public static OverlayReport Collect(OverlayWindow? overlay)
    {
        var report = new OverlayReport();
        if (overlay is null)
        {
            Trace.Log("probe: overlay is null");
            return report;
        }

        report.OverlayCreated = true;

        // -- Section 1: extended styles + window rect (read back from Win32) --
        try
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(overlay);
            long ex = OverlayNative.GetExStyle(hwnd);
            report.Topmost = (ex & OverlayNative.WS_EX_TOPMOST) != 0;
            report.ClickThrough = (ex & OverlayNative.WS_EX_TRANSPARENT) != 0;
            report.NoActivate = (ex & OverlayNative.WS_EX_NOACTIVATE) != 0;
            report.ToolWindow = (ex & OverlayNative.WS_EX_TOOLWINDOW) != 0;

            if (OverlayNative.GetWindowRect(hwnd, out OverlayNative.RECT wr))
            {
                report.WindowRect =
                    $"{wr.Left},{wr.Top} {wr.Right - wr.Left}×{wr.Bottom - wr.Top}";
            }
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"styles: {ex.GetType().Name}: {ex.Message}");
            Trace.Log($"probe: styles section FAILED: {ex.Message}");
        }

        // -- Section 2: fullscreen check (window rect vs. its monitor rect).
        //      Classic Win32 only: DisplayArea.FindAll() has a known
        //      InvalidCastException issue in the WinAppSDK projection. --
        try
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(overlay);
            IntPtr hmon = OverlayNative.MonitorFromWindow(
                hwnd, OverlayNative.MONITOR_DEFAULTTONEAREST);
            var info = new OverlayNative.MONITORINFO
            {
                cbSize = Marshal.SizeOf<OverlayNative.MONITORINFO>(),
            };
            if (hmon != IntPtr.Zero && OverlayNative.GetMonitorInfo(hmon, ref info))
            {
                OverlayNative.RECT m = info.rcMonitor;
                report.ScreenRect = $"{m.Left},{m.Top} {m.Right - m.Left}×{m.Bottom - m.Top}";

                if (OverlayNative.GetWindowRect(hwnd, out OverlayNative.RECT wr))
                {
                    report.Fullscreen =
                        wr.Left == m.Left
                        && wr.Top == m.Top
                        && (wr.Right - wr.Left) == (m.Right - m.Left)
                        && (wr.Bottom - wr.Top) == (m.Bottom - m.Top);
                }
            }
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"fullscreen: {ex.GetType().Name}: {ex.Message}");
            Trace.Log($"probe: fullscreen section FAILED: {ex.Message}");
        }

        // -- Section 3: multi-monitor enumeration (classic Win32) --
        try
        {
            var rects = new List<string>();
            int count = 0;
            OverlayNative.EnumDisplayMonitors(
                IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdc, ref OverlayNative.RECT rect, IntPtr data) =>
                {
                    var mi = new OverlayNative.MONITORINFO
                    {
                        cbSize = Marshal.SizeOf<OverlayNative.MONITORINFO>(),
                    };
                    if (OverlayNative.GetMonitorInfo(hMonitor, ref mi))
                    {
                        OverlayNative.RECT m = mi.rcMonitor;
                        rects.Add($"{m.Left},{m.Top} {m.Right - m.Left}×{m.Bottom - m.Top}");
                    }
                    count++;
                    return true;
                },
                IntPtr.Zero);
            report.MonitorCount = count;
            report.Monitors = string.Join("; ", rects);
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"monitors: {ex.GetType().Name}: {ex.Message}");
            Trace.Log($"probe: monitors section FAILED: {ex.Message}");
        }

        // -- Section 4: transparency flag + capture counters --
        try
        {
            report.TransparentDwm = overlay.DwmExtended;
            report.CaptureFrames = overlay.Renderer?.PresentedFrames ?? 0;
            report.CaptureState = overlay.CaptureState;
            report.Warnings.AddRange(overlay.Warnings);
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"capture: {ex.GetType().Name}: {ex.Message}");
            Trace.Log($"probe: capture section FAILED: {ex.Message}");
        }

        return report;
    }

    public static void WriteSmokeJson(OverlayReport report)
    {
        string path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "duoflow-overlay-smoke.json");
        System.IO.File.WriteAllText(
            path,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
