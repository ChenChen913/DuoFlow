using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DuoFlow.App;

/// <summary>
/// P0-1: removes the opaque base of the XAML island (the second of the two
/// background layers of a WinUI 3 window; DwmExtendFrameIntoClientArea only
/// handles the Win32 one).
///
/// Implementation follows cnbluefire/WinUI3TransparentBackground verbatim
/// (fetched 2026-09-16) - do NOT "simplify" it back:
///   1. DwmEnableBlurBehindWindow with an empty region (-2,-2)-(-1,-1)
///      marks the window for DWM alpha compositing;
///   2. the WINDOW OBJECT ITSELF is cast to the OS interface
///      (WinRT CastExtensions.As&lt;Windows.UI.Composition.
///      ICompositionSupportsSystemBackdrop&gt;) and an alpha-0 brush is
///      assigned directly. This BYPASSES the Microsoft.UI.Xaml.Media.
///      SystemBackdrop subclass mechanism: a custom SystemBackdrop subclass
///      got its OnTargetConnected and connected an identical alpha-0 brush
///      (CI run 35077212761) yet the screen stayed black, while the direct
///      interface path is the one actually shipped in production by the
///      reference implementation;
///   3. the compositor MUST be a fresh Windows.UI.Composition.Compositor
///      built after ensuring a WINDOWS.SYSTEM DispatcherQueue
///      (EnsureWindowsSystemDispatcherQueueController, official helper with
///      DQTAT_COM_STA). Microsoft.UI.Composition.* (XAML's own compositor)
///      is a DIFFERENT WinRT runtime class - casts and CsWinRT As&lt;T&gt;
///      re-wraps of its objects fail (runs 35075895577/35076653477);
///   4. WM_PAINT is subclassed to fill the Win32 surface black and skip
///      default painting (the empty-region blur-behind turns the DWM state
///      into per-pixel alpha over the desktop).
///
/// Known sharp edge (microsoft-ui-xaml#1247): layered + SwapChainPanel +
/// transparent is a long-standing problem combination - transparency,
/// click-through and the capture preview must be re-verified TOGETHER.
/// </summary>
internal static class OverlayTransparency
{
    private static Windows.UI.Composition.Compositor? _compositor;

    /// <summary>
    /// Applies the full island-layer transparency recipe to the overlay
    /// window. Returns true when the alpha-0 backdrop brush is connected.
    /// </summary>
    public static bool Apply(Window window)
    {
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        // 1. DWM per-pixel alpha enablement (empty blur region).
        int hr = OverlayNative.ApplyTransparentWin32Layer(hwnd);
        Trace.Log($"transparency: blur-behind applied (hr={hr})");

        // 2. Alpha-0 brush on the window's OS backdrop interface.
        try
        {
            Windows.UI.Composition.Compositor compositor = EnsureCompositor();
            var brushHolder = WinRT.CastExtensions.As<Windows.UI.Composition.ICompositionSupportsSystemBackdrop>(window);
            brushHolder.SystemBackdrop =
                compositor.CreateColorBrush(Windows.UI.Color.FromArgb(0, 255, 255, 255));
            Trace.Log("transparency: alpha-0 backdrop brush connected via window.As<>");
            return true;
        }
        catch (Exception ex)
        {
            Trace.Log($"transparency: backdrop brush FAILED: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// The OS compositor requires an OS-level (Windows.System)
    /// DispatcherQueue on the calling thread; the WinUI 3 UI thread only has
    /// the Microsoft.UI.Dispatching one (CI-verified), so the official
    /// CoreMessaging helper runs first.
    /// </summary>
    private static Windows.UI.Composition.Compositor EnsureCompositor()
    {
        if (_compositor == null)
        {
            if (!OverlayNative.EnsureWindowsSystemDispatcherQueueController())
            {
                throw new InvalidOperationException(
                    "Could not ensure a Windows.System DispatcherQueue for Windows.UI.Composition.Compositor.");
            }
            _compositor = new Windows.UI.Composition.Compositor();
        }
        return _compositor;
    }
}

/// <summary>
/// Win32 message subclass for the overlay top-level window:
///   - WM_PAINT: fill the Win32 surface black and skip default painting.
///     With the empty-region blur-behind the DWM treats the window as
///     per-pixel alpha; the island's transparent regions then show the
///     desktop. (cnbluefire's WndProc does exactly this.)
///   - WM_DWMCOMPOSITIONCHANGED: re-apply the DWM transparency state,
///     because DWM resets it when composition toggles (RDP, driver reset).
///   (Note: WM_PAINT is 0x000F - an earlier draft mislabeled it as
///   WM_ERASEBKGND, which is 0x0014.)
/// </summary>
internal static class OverlayWin32Subclass
{
    // Keep the delegate rooted for the window's lifetime (GC must not
    // collect the callback while the native subclass is installed).
    private static OverlayNative.SubclassProc? _proc;
    private static IntPtr _blackBrush = IntPtr.Zero;

    /// <summary>Unique subclass id for DuoFlow's overlay window.</summary>
    private const uint SubclassId = 0x4D4630; // "DF0"

    public static void Install(IntPtr hwnd)
    {
        _proc = Handler;
        if (!OverlayNative.SetWindowSubclass(hwnd, _proc, SubclassId, IntPtr.Zero))
        {
            Trace.Log("subclass: SetWindowSubclass FAILED");
        }
        else
        {
            Trace.Log("subclass: installed (WM_PAINT black / WM_DWMCOMPOSITIONCHANGED)");
        }
    }

    private static IntPtr Handler(
        IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        if (msg == OverlayNative.WM_PAINT)
        {
            if (OverlayNative.BeginPaint(hwnd, out OverlayNative.PAINTSTRUCT ps).ToInt64() != 0)
            {
                if (_blackBrush == IntPtr.Zero)
                {
                    _blackBrush = OverlayNative.GetStockObject(4 /* BLACK_BRUSH */);
                }
                OverlayNative.FillRect(ps.hdc, ref ps.rcPaint, _blackBrush);
                OverlayNative.EndPaint(hwnd, in ps);
            }
            return new IntPtr(1); // skip default painting
        }

        if (msg == 0x031E) // WM_DWMCOMPOSITIONCHANGED
        {
            int hr = OverlayNative.ApplyTransparentWin32Layer(hwnd);
            OverlayNative.InvalidateRect(hwnd, IntPtr.Zero, true);
            Trace.Log($"subclass: WM_DWMCOMPOSITIONCHANGED -> re-applied (hr={hr})");
        }

        return OverlayNative.DefSubclassProc(hwnd, msg, wParam, lParam);
    }
}
