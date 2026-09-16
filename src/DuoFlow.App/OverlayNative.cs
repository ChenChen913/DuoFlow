using System;
using System.Runtime.InteropServices;

namespace DuoFlow.App;

/// <summary>
/// Win32 interop for the M0.3 overlay window: extended styles
/// (click-through / no-activate / tool-window / topmost) and DWM
/// frame extension used for a fully transparent client area.
/// </summary>
public static class OverlayNative
{
    public const int GWL_EXSTYLE = -20;

    public const long WS_EX_TOPMOST = 0x00000008;
    public const long WS_EX_TRANSPARENT = 0x00000020;
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_NOACTIVATE = 0x08000000;
    // P0-2 (real-machine finding, 2026-09-16): WS_EX_TRANSPARENT alone does
    // NOT take a WinUI 3 content island out of the hit-test chain - the
    // DesktopChildSiteBridge still swallows every click. WS_EX_LAYERED is
    // what makes transparent + topmost windows excluded from hit-testing.
    public const long WS_EX_LAYERED = 0x00080000;

    public const uint LWA_ALPHA = 0x00000002;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    // ---- P0-1 Win32-layer transparency recipe (castorix/
    //      WinUI3_SwapChainPanel_Layered; verified against the real machine
    //      finding that DwmExtendFrame(-1) alone leaves the XAML island
    //      opaque black) ----

    private const uint DWM_BB_ENABLE = 0x00000001;
    private const uint DWM_BB_BLURREGION = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        public bool fEnable;
        public IntPtr hRgnBlur;
        public int fTransitionOnMaximized;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("gdi32.dll")]
    public static extern IntPtr GetStockObject(int fnObject);

    [DllImport("user32.dll")]
    public static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hBrush);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    // Empty blur region: marks the window blur-enabled so the alpha-0
    // backdrop region composites through DWM, while the degenerate region
    // (-2,-2)-(-1,-1) keeps any real blur from being drawn.
    private static IntPtr _emptyBlurRegion = IntPtr.Zero;

    /// <summary>
    /// P0-1 Win32 layer: DwmExtendFrameIntoClientArea with MARGINS(0) - NOT
    /// the legacy -1 - plus DwmEnableBlurBehindWindow with an empty region.
    /// Returns the ExtendFrame HRESULT (0 == success).
    /// </summary>
    public static int ApplyTransparentWin32Layer(IntPtr hwnd)
    {
        var margins = new MARGINS(); // all zero - legacy -1 does not help here
        int hr = DwmExtendFrameIntoClientArea(hwnd, ref margins);

        if (_emptyBlurRegion == IntPtr.Zero)
        {
            _emptyBlurRegion = CreateRectRgn(-2, -2, -1, -1);
        }

        var bb = new DWM_BLURBEHIND
        {
            dwFlags = DWM_BB_ENABLE | DWM_BB_BLURREGION,
            fEnable = true,
            hRgnBlur = _emptyBlurRegion,
        };
        _ = DwmEnableBlurBehindWindow(hwnd, ref bb);
        return hr;
    }

    /// <summary>
    /// P0-2: add WS_EX_LAYERED on top of the existing WS_EX_TRANSPARENT and
    /// initialize the layered attributes once (a layered window whose
    /// attributes were never set is not composited at all), then force a
    /// frame change so the new ex-style takes effect immediately.
    /// </summary>
    public static void EnableLayeredClickThrough(IntPtr hwnd)
    {
        long ex = GetExStyle(hwnd);
        SetExStyle(hwnd, ex | WS_EX_LAYERED);
        _ = SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
        ForceTopmost(hwnd); // includes SWP_FRAMECHANGED
    }

    // ---- Real-effect verification helpers (anti-false-positive work,
    //      2026-09-16): screen sampling + hit-test chain queries ----

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    public const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    // ---- comctl32 window subclassing (WM_ERASEBKGND / WM_DWMCOMPOSITIONCHANGED
    //      for the P0-1 Win32-layer transparency recipe) ----

    public delegate IntPtr SubclassProc(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll")]
    public static extern bool SetWindowSubclass(
        IntPtr hWnd, SubclassProc pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll")]
    public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    // ---- Luminance reference window (real-effect transparency probe):
    //      a plain (non-topmost) white window that sits BELOW the topmost
    //      overlay; if the overlay is truly transparent its white shows
    //      through the sample rect, otherwise the sample stays black. ----

    public const uint CS_HREDRAW = 0x0002;
    public const uint CS_VREDRAW = 0x0001;
    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int WS_VISIBLE = 0x10000000;
    public const int WS_EX_NOACTIVATE_INT = 0x08000000;
    public const int WS_EX_TOOLWINDOW_INT = 0x00000080;
    public const int COLOR_WINDOW = 5;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSW
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowExW(
        int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandleW(string? lpModuleName);

    public delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // ---- OS-level DispatcherQueue (CoreMessaging) for Windows.UI.Composition.
    //      Verbatim-port of the official WindowsSystemDispatcherQueueHelper
    //      (Windows App SDK system-backdrop docs + castorix recipe). Critical
    //      details that broke earlier attempts (CI-verified):
    //      apartmentType MUST be DQTAT_COM_STA (2) - DQTAT_COM_NONE (0)
    //      creates a queue that Windows.UI.Composition.Compositor still
    //      rejects with Access is denied (runs 35075183532/35075895577) -;
    //      the controller out-param marshals as IUnknown object; and
    //      GetForCurrentThread() returns null (not throw) when absent. ----

    private static object? _osDispatcherQueueController; // rooted for process lifetime

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(
        [In] DispatcherQueueOptions options,
        [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

    /// <summary>
    /// Ensures an OS-level (Windows.System) DispatcherQueue exists on the
    /// calling thread so Windows.UI.Composition.Compositor can be constructed.
    /// </summary>
    public static bool EnsureWindowsSystemDispatcherQueueController()
    {
        try
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                return true; // one already exists, so we'll just use it
            }
        }
        catch
        {
            // projection may throw instead of returning null - fall through
        }

        if (_osDispatcherQueueController == null)
        {
            var options = new DispatcherQueueOptions
            {
                dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
                threadType = 2,    // DQTYPE_THREAD_CURRENT
                apartmentType = 2, // DQTAT_COM_STA
            };
            object controller = _osDispatcherQueueController!;
            int hr = CreateDispatcherQueueController(options, ref controller);
            if (hr != 0)
            {
                Trace.Log($"EnsureWindowsSystemDispatcherQueueController: hr=0x{hr:X8}");
                return false;
            }
            _osDispatcherQueueController = controller; // keep alive
        }
        return true;
    }

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    public const uint SRCCOPY = 0x00CC0020;
    public const uint BI_RGB = 0;

    /// <summary>
    /// Samples the AVERAGE luminance (0~255) of a screen rectangle from the
    /// final composited desktop (BitBlt from the screen DC, CAPTUREBLT not
    /// needed - real-machine testing showed identical results either way).
    /// Returns -1 on failure.
    /// </summary>
    public static double SampleScreenLuminance(int x, int y, int width, int height)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return -1;
        }

        try
        {
            var bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // top-down
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = BI_RGB;

            IntPtr memDc = CreateCompatibleDC(screenDc);
            if (memDc == IntPtr.Zero)
            {
                return -1;
            }

            try
            {
                if (CreateDIBSection(memDc, ref bmi, 0 /* DIB_RGB_COLORS */, out IntPtr bits, IntPtr.Zero, 0) == IntPtr.Zero)
                {
                    return -1;
                }

                IntPtr old = SelectObject(memDc, bits);
                bool ok = BitBlt(memDc, 0, 0, width, height, screenDc, x, y, SRCCOPY);
                SelectObject(memDc, old);
                if (!ok)
                {
                    return -1;
                }

                // Copy the DIB out instead of pointer arithmetic (keeps the
                // project free of AllowUnsafeBlocks); 32bpp top-down BGRA.
                int stride = width * 4;
                byte[] px = new byte[stride * height];
                Marshal.Copy(bits, px, 0, px.Length);

                double total = 0;
                for (int row = 0; row < height; row++)
                {
                    int o = row * stride;
                    for (int col = 0; col < width; col++)
                    {
                        int i = o + col * 4;
                        total += (px[i] + px[i + 1] + px[i + 2]) / 3.0; // B, G, R
                    }
                }
                long count = (long)width * height;
                return count == 0 ? -1 : total / count;
            }
            finally
            {
                DeleteDC(memDc);
            }
        }
        finally
        {
            _ = ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    // ---- Classic Win32 monitor enumeration (avoids the DisplayArea.FindAll
    //      WinRT projection, which throws InvalidCastException on some
    //      Windows App SDK versions) ----

    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint MONITORINFOF_PRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public delegate bool MonitorEnumProc(
        IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public static long GetExStyle(IntPtr hwnd)
        => IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, GWL_EXSTYLE).ToInt64()
            : GetWindowLong32(hwnd, GWL_EXSTYLE);

    public static void SetExStyle(IntPtr hwnd, long exStyle)
    {
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
        }
        else
        {
            SetWindowLong32(hwnd, GWL_EXSTYLE, unchecked((int)exStyle));
        }
    }

    /// <summary>Force the window into the topmost band (idempotent).</summary>
    public static void ForceTopmost(IntPtr hwnd)
        => SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);

    /// <summary>
    /// Extends the DWM frame over the whole client area (-1 margins) so the
    /// XAML-transparent regions of the window become truly see-through.
    /// Returns the HRESULT (0 == success).
    /// </summary>
    public static int ExtendFrame(IntPtr hwnd)
    {
        var margins = new MARGINS
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1,
        };
        return DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }
}
