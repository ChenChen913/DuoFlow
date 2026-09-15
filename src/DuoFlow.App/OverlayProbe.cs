using System;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Windowing;

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

    public System.Collections.Generic.List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Collects the overlay window properties (styles are read back from the
/// actual Win32 window, not from our intentions) and persists the smoke
/// result for the CI acceptance gate.
/// </summary>
public static class OverlayProbe
{
    public static OverlayReport Collect(OverlayWindow? overlay)
    {
        var report = new OverlayReport();
        if (overlay is null)
        {
            return report; // OverlayCreated = false
        }

        report.OverlayCreated = true;

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(overlay);
        long ex = OverlayNative.GetExStyle(hwnd);
        report.Topmost = (ex & OverlayNative.WS_EX_TOPMOST) != 0;
        report.ClickThrough = (ex & OverlayNative.WS_EX_TRANSPARENT) != 0;
        report.NoActivate = (ex & OverlayNative.WS_EX_NOACTIVATE) != 0;
        report.ToolWindow = (ex & OverlayNative.WS_EX_TOOLWINDOW) != 0;

        // Window rect vs. primary screen rect -> "fullscreen" means the
        // overlay exactly covers the physical monitor.
        Windows.Graphics.RectInt32 screen = Microsoft.UI.Windowing.DisplayArea.Primary.OuterBounds;
        report.ScreenRect = $"{screen.X},{screen.Y} {screen.Width}×{screen.Height}";
        if (OverlayNative.GetWindowRect(hwnd, out OverlayNative.RECT wr))
        {
            int w = wr.Right - wr.Left;
            int h = wr.Bottom - wr.Top;
            report.WindowRect = $"{wr.Left},{wr.Top} {w}×{h}";
            report.Fullscreen = wr.Left == screen.X
                && wr.Top == screen.Y
                && w == screen.Width
                && h == screen.Height;
        }

        report.TransparentDwm = overlay.DwmExtended;

        // Multi-monitor enumeration (code-path verification; a real
        // multi-monitor layout is a true-machine follow-up).
        var areas = DisplayArea.FindAll();
        report.MonitorCount = areas.Count;
        report.Monitors = string.Join("; ", areas.Select(a =>
            $"{a.OuterBounds.X},{a.OuterBounds.Y} {a.OuterBounds.Width}×{a.OuterBounds.Height}"));

        report.CaptureFrames = overlay.Renderer?.PresentedFrames ?? 0;
        report.CaptureState = overlay.CaptureState;
        report.Warnings = new System.Collections.Generic.List<string>(overlay.Warnings);

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
