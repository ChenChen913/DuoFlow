using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace DuoFlow.App;

/// <summary>
/// P0-1 real-effect proof: a white reference window is created BELOW the
/// topmost overlay; its area is sampled from the composited screen. If the
/// overlay is truly transparent the white shows through (luminance ≈ 255);
/// the old false-positive (style/API check only) read ≈ 0 on the real
/// machine because the opaque island hid everything.
///
/// 2026-09-16 (real-machine re-verification round): the probe itself was
/// reporting a FALSE NEGATIVE there - lum=0 while independent same-run
/// samplers proved the screen WAS transparent. The old attribution ("cloud
/// RDP sessions don't composite per-pixel alpha") is RETRACTED: the real
/// machine is a physical console session (SM_REMOTESESSION=0) and still
/// read 0 with the old probe. Leading explanation: the old flow blocked the
/// UI thread with Thread.Sleep(450) right after a z-order change, starving
/// re-composition - so DWM composited the black backdrop and BitBlt read 0.
/// The flow is now ASYNC (the wait no longer blocks the UI thread), the
/// overlay is repainted explicitly before sampling, and every GDI stage is
/// traced. Two control paths (background-thread BitBlt + GetPixel spots)
/// are sampled against the same rect in the same run. This probe stays
/// INFORMATIONAL ONLY - the transparency authority remains the real-machine
/// sampling comparison (overlay-on vs overlay-away, DD-037).
/// </summary>
public sealed class TransparencyProbe
{
    public string Method { get; set; } =
        "BitBlt screen sample of a white reference window placed UNDER the topmost overlay";

    /// <summary>UI-thread sample after the async wait (the headline number).</summary>
    public double ReferenceLuminance { get; set; }

    /// <summary>Control: same rect, same moment, sampled on a background thread.</summary>
    public double BackgroundThreadLuminance { get; set; } = -1;

    /// <summary>Control: GetPixel spot checks (center + corners of the sample rect).</summary>
    public string GetPixelRgb { get; set; } = "";

    /// <summary>Control: GetPixel just OUTSIDE the reference window (calibration point).</summary>
    public string GetPixelOutsideRgb { get; set; } = "";

    /// <summary>
    /// True when ALL GetPixel spots INSIDE the reference window are bright
    /// (min channel &gt;= 240). INFORMATIONAL ONLY: not promoted to the gate
    /// yet - a capture path that excludes layered windows would also read
    /// white here even if the overlay were opaque (the reference window is
    /// itself a plain GDI window). Needs the console-point calibration on
    /// the real machine first (DD-037 erratum).
    /// </summary>
    public bool GetPixelPass { get; set; }

    public string SampleRect { get; set; } = "";

    /// <summary>Primary/virtual-screen geometry + system DPI (coordinate-normalization traps).</summary>
    public string ScreenGeometry { get; set; } = "";

    /// <summary>False when the GDI chain itself failed (see Diagnostics for where).</summary>
    public bool BitBltOk { get; set; }

    /// <summary>First 8 raw DIB bytes - all zero means "DWM handed us a black frame".</summary>
    public string DibFirst8Bytes { get; set; } = "";

    public double PassThreshold { get; set; } = 80;
    public bool Pass { get; set; }
    public string Note { get; set; } = "";

    /// <summary>Per-stage GDI trace (also mirrored into duoflow-trace.txt).</summary>
    public List<string> Diagnostics { get; set; } = new();
}

/// <summary>
/// P0-2 hit-test chain proof: WindowFromPoint at a covered console point
/// must NOT resolve to the overlay's DesktopChildSiteBridge anymore once
/// WS_EX_LAYERED is in place (root window != overlay hwnd).
/// </summary>
public sealed class HitTestProbe
{
    public string Method { get; set; } =
        "WindowFromPoint at the covered console center, compared against the overlay root hwnd";

    public string WindowFromPointClass { get; set; } = "";
    public bool PointsToOverlayChild { get; set; }
    public bool Pass { get; set; }
    public string Note { get; set; } =
        "API-level proof (run in CI); real-input SendInput click/drag through the overlay is verified separately on the real machine";
}

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

    // P0 real-effect probes (2026-09-16): the style-bit booleans above only
    // prove intent; these prove BEHAVIOR. CI gates on the HIT-TEST probe;
    // the transparency luminance is INFORMATIONAL ONLY (known false-negative
    // history - UI-thread starvation in the old synchronous flow, DD-037);
    // the transparency authority is the real-machine sampling comparison.
    public TransparencyProbe Transparency { get; set; } = new();
    public HitTestProbe HitTest { get; set; } = new();

    // P0 fix flags read back from the actual window.
    public bool LayeredApplied { get; set; }
    public bool BackdropApplied { get; set; }

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

        // -- Section 5: P0 fix flags + REAL-EFFECT checks --
        // The style booleans in Sections 1-4 only prove that bits were set
        // and APIs returned S_OK - exactly the false positives that shipped
        // M0.3. This section proves behavior: luminance under the overlay
        // (P0-1) and the hit-test chain (P0-2).
        report.LayeredApplied = overlay.LayeredApplied;
        report.BackdropApplied = overlay.BackdropApplied;
        try
        {
            EnsureRealEffectChecks(overlay);
            report.Transparency = _transparency;
            report.HitTest = _hitTest;
        }
        catch (Exception ex)
        {
            report.Warnings.Add($"real-effect: {ex.GetType().Name}: {ex.Message}");
            Trace.Log($"probe: real-effect section FAILED: {ex.Message}");
            report.Transparency.Note = $"probe error: {ex.Message}";
            report.HitTest.Note = $"probe error: {ex.Message}";
        }

        return report;
    }

    // ------------------------------------------------------------------
    // Real-effect checks (run ONCE per process - they flip the TOPMOST
    // z-order, which would visibly flicker every second otherwise).
    //
    // ASYNC since the 2026-09-16 false-negative round: the old version was
    // synchronous and did Thread.Sleep(450) on the UI thread right after
    // ForceTopmost - the leading explanation for the constant lum=0 (the
    // blocked UI thread starves re-composition; DD-037). The wait is now
    // `await Task.Delay`, the overlay is repainted explicitly, and every
    // GDI stage is traced into TransparencyProbe.Diagnostics.
    // ------------------------------------------------------------------

    private static bool _realEffectStarted;
    private static TransparencyProbe _transparency = new();
    private static HitTestProbe _hitTest = new();

    private const string RefWindowClassName = "DuoFlowLumaRef";

    /// <summary>
    /// Kicks the real-effect checks off ONCE (fire-and-forget async flow);
    /// later Collect calls just copy the live probe objects. Until the flow
    /// completes, the note reads "running" - the smoke JSON is rewritten
    /// every second, so the final values reach the CI gate anyway.
    /// </summary>
    private static void EnsureRealEffectChecks(OverlayWindow overlay)
    {
        if (_realEffectStarted)
        {
            return;
        }
        _realEffectStarted = true;

        _transparency.Note = "real-effect probe running (async; UI thread stays free)";
        Trace.Log("probe: real-effect checks START (async)");
        _ = RunRealEffectChecksAsync(overlay);
    }

    private static async Task RunRealEffectChecksAsync(OverlayWindow overlay)
    {
        IntPtr overlayHwnd = WinRT.Interop.WindowNative.GetWindowHandle(overlay);
        IntPtr refWindow = IntPtr.Zero;

        try
        {
            // 1) White reference window, plain (non-topmost) z-order -> below
            //    both topmost windows. NOACTIVATE + TOOLWINDOW so it never
            //    steals focus or shows up in Alt+Tab.
            refWindow = CreateLumaReferenceWindow(overlayHwnd, out OverlayNative.RECT refRect);
            if (refWindow == IntPtr.Zero)
            {
                _transparency.Note = "reference window creation failed - transparency check skipped (informational only)";
                Trace.Log("probe: transparency check SKIPPED (no reference window)");
                return;
            }

            // 2) Find the console window (the control panel that must stay
            //    clickable THROUGH the overlay once P0-2 is fixed).
            IntPtr consoleHwnd = FindWindowByTitlePrefix("DuoFlow Console");

            // 3) Raise the overlay to the top of the TOPMOST band - the
            //    production posture (console normally sits above it as a
            //    debug panel, see P1-b in EXECUTION_PLAN §5) - then force a
            //    repaint so DWM has fresh content to composite.
            OverlayNative.ForceTopmost(overlayHwnd);
            _ = OverlayNative.InvalidateRect(overlayHwnd, IntPtr.Zero, true);
            _ = OverlayNative.UpdateWindow(overlayHwnd);

            // 4) Wait WITHOUT blocking the UI thread. The old
            //    Thread.Sleep(450) is the prime suspect for the constant
            //    lum=0 (blocked UI thread starves re-composition, DD-037);
            //    Task.Delay lets the island pump messages and DWM composite
            //    while we wait.
            int uiThread = (int)OverlayNative.GetCurrentThreadId();
            Trace.Log($"probe: waiting 450ms via await Task.Delay (UI thread {uiThread} stays free)");
            await Task.Delay(450);
            Trace.Log($"probe: wait done (thread now {OverlayNative.GetCurrentThreadId()})");

            // 5) P0-1 proof: sample the composited screen where the white
            //    reference window sits under the topmost overlay. Fully
            //    instrumented (every GDI stage traced) + two control paths
            //    (background-thread BitBlt, GetPixel spots) on the same rect.
            int sx = (refRect.Left + refRect.Right) / 2 - 50;
            int sy = (refRect.Top + refRect.Bottom) / 2 - 30;
            var stages = new List<string>();
            double lum = OverlayNative.SampleScreenLuminance(sx, sy, 100, 60, stages);

            _transparency.SampleRect = $"{sx},{sy} 100x60";
            _transparency.ScreenGeometry = OverlayNative.DescribeScreenGeometry();
            _transparency.BitBltOk = lum >= 0;
            _transparency.Diagnostics.AddRange(stages);
            _transparency.DibFirst8Bytes =
                stages.Find(s => s.StartsWith("sample: dib[0..8]=", StringComparison.Ordinal)) ?? "";
            _transparency.BackgroundThreadLuminance = await Task.Run(
                () => OverlayNative.SampleScreenLuminance(sx, sy, 100, 60));

            IntPtr screenDc = OverlayNative.GetDC(IntPtr.Zero);
            try
            {
                string c = OverlayNative.SamplePointViaGetPixel(screenDc, sx + 50, sy + 30);
                string tl = OverlayNative.SamplePointViaGetPixel(screenDc, sx + 5, sy + 5);
                string br = OverlayNative.SamplePointViaGetPixel(screenDc, sx + 95, sy + 55);
                _transparency.GetPixelRgb = $"center={c} topleft={tl} bottomright={br}";
                _transparency.GetPixelOutsideRgb = OverlayNative.SamplePointViaGetPixel(
                    screenDc, (refRect.Left + refRect.Right) / 2, refRect.Bottom + 40);
                _transparency.GetPixelPass =
                    MinChannel(c) >= 240 && MinChannel(tl) >= 240 && MinChannel(br) >= 240;
            }
            finally
            {
                _ = OverlayNative.ReleaseDC(IntPtr.Zero, screenDc);
            }

            _transparency.ReferenceLuminance = lum;
            _transparency.Pass = lum >= _transparency.PassThreshold;
            _transparency.Note = lum >= _transparency.PassThreshold
                ? $"BitBlt sees the white reference (bgThread={_transparency.BackgroundThreadLuminance:0.0}, outside={_transparency.GetPixelOutsideRgb})"
                : _transparency.GetPixelPass
                    ? $"GetPixel shows the white reference THROUGH the overlay (inside={_transparency.GetPixelRgb}); BitBlt reading black over the overlay region is a known in-process capture artifact (DD-037 erratum) - informational only"
                    : $"BitBlt=black AND GetPixel!=white (inside={_transparency.GetPixelRgb} outside={_transparency.GetPixelOutsideRgb}) - opaque overlay OR capture artifact; informational only, transparency authority = real-machine sampling (DD-037)";
            Trace.Log($"probe: transparency lum={lum:0.0} bgThread={_transparency.BackgroundThreadLuminance:0.0} getPixelPass={_transparency.GetPixelPass} getPixel[{_transparency.GetPixelRgb}] outside={_transparency.GetPixelOutsideRgb} pass={_transparency.Pass}");

            // 6) P0-2 proof (API level): hit-testing through the overlay.
            if (consoleHwnd != IntPtr.Zero
                && OverlayNative.GetWindowRect(consoleHwnd, out OverlayNative.RECT consoleRect))
            {
                var pt = new OverlayNative.POINT
                {
                    X = (consoleRect.Left + consoleRect.Right) / 2,
                    Y = (consoleRect.Top + consoleRect.Bottom) / 2,
                };
                IntPtr hit = OverlayNative.WindowFromPoint(pt);
                IntPtr root = OverlayNative.GetAncestor(hit, OverlayNative.GA_ROOT);

                var className = new System.Text.StringBuilder(256);
                _ = OverlayNative.GetClassName(hit, className, 256);
                _hitTest.WindowFromPointClass = className.ToString();
                _hitTest.PointsToOverlayChild = root == overlayHwnd;
                _hitTest.Pass = !_hitTest.PointsToOverlayChild;
                Trace.Log($"probe: hit-test class={_hitTest.WindowFromPointClass} pass={_hitTest.Pass}");
            }
            else
            {
                _hitTest.Note = "console window not found - hit-test check skipped (counts as failure)";
                Trace.Log("probe: hit-test console window NOT FOUND");
            }

            // 7) Restore the debug layout: console above the overlay again.
            if (consoleHwnd != IntPtr.Zero)
            {
                OverlayNative.ForceTopmost(consoleHwnd);
            }
        }
        catch (Exception ex)
        {
            _transparency.Note = $"probe error: {ex.GetType().Name}: {ex.Message}";
            _hitTest.Note = $"probe error: {ex.GetType().Name}: {ex.Message}";
            Trace.Log($"probe: real-effect checks FAILED: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (refWindow != IntPtr.Zero)
            {
                _ = OverlayNative.DestroyWindow(refWindow);
            }
            Trace.Log("probe: real-effect checks END");
        }
    }

    // Min channel of an "R,G,B" string; -1 when unparseable (e.g. "failed")
    // so that unparseable results never count as bright.
    private static int MinChannel(string rgb)
    {
        if (string.IsNullOrEmpty(rgb))
        {
            return -1;
        }
        int min = 255;
        foreach (string part in rgb.Split(','))
        {
            if (!int.TryParse(part, out int v))
            {
                return -1;
            }
            min = Math.Min(min, v);
        }
        return min;
    }

    // Rooted delegate for the reference window's wndproc (GC must not
    // collect it while the window class is alive).
    private static readonly OverlayNative.WndProcDelegate _refWndProc =
        (hwnd, msg, wParam, lParam) => OverlayNative.DefWindowProcW(hwnd, msg, wParam, lParam);

    private static IntPtr CreateLumaReferenceWindow(IntPtr overlayHwnd, out OverlayNative.RECT rect)
    {
        rect = default;

        var wndClass = new OverlayNative.WNDCLASSW
        {
            style = OverlayNative.CS_HREDRAW | OverlayNative.CS_VREDRAW,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_refWndProc),
            hInstance = OverlayNative.GetModuleHandleW(null),
            hbrBackground = OverlayNative.GetStockObject(0 /* WHITEBRUSH */),
            lpszClassName = RefWindowClassName,
        };
        ushort atom = OverlayNative.RegisterClassW(ref wndClass);
        if (atom == 0)
        {
            Trace.Log("probe: RegisterClassW(DuoFlowLumaRef) failed (maybe registered already)");
        }

        // Place it center-left-upper: away from the console (bottom-left),
        // the capture preview (bottom-right) and the status chip (top-right).
        // All coordinates are proportional, so the CI runner's 1024x768 and
        // the real machine's 1920x1080 both work.
        if (!OverlayNative.GetWindowRect(overlayHwnd, out OverlayNative.RECT overlayRect))
        {
            return IntPtr.Zero;
        }

        int w = 320, h = 220;
        int x = overlayRect.Left + (overlayRect.Right - overlayRect.Left) * 35 / 100;
        int y = overlayRect.Top + (overlayRect.Bottom - overlayRect.Top) * 28 / 100;

        IntPtr hwnd = OverlayNative.CreateWindowExW(
            OverlayNative.WS_EX_NOACTIVATE_INT | OverlayNative.WS_EX_TOOLWINDOW_INT,
            RefWindowClassName,
            "DuoFlow LumaRef",
            OverlayNative.WS_POPUP | OverlayNative.WS_VISIBLE,
            x, y, w, h,
            IntPtr.Zero, IntPtr.Zero, OverlayNative.GetModuleHandleW(null), IntPtr.Zero);

        if (hwnd != IntPtr.Zero && OverlayNative.GetWindowRect(hwnd, out rect))
        {
            Trace.Log($"probe: luma reference window at {x},{y} {w}×{h}");
        }
        else
        {
            Trace.Log("probe: luma reference window creation FAILED");
        }
        return hwnd;
    }

    private static IntPtr FindWindowByTitlePrefix(string prefix)
    {
        IntPtr found = IntPtr.Zero;
        OverlayNative.EnumWindows((hwnd, _) =>
        {
            if (!OverlayNative.IsWindowVisible(hwnd))
            {
                return true;
            }

            var title = new System.Text.StringBuilder(256);
            _ = OverlayNative.GetWindowText(hwnd, title, 256);
            if (title.ToString().StartsWith(prefix, StringComparison.Ordinal))
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
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
