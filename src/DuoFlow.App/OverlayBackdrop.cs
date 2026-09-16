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
        }
        catch (Exception ex)
        {
            Trace.Log($"transparency: backdrop brush FAILED: {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        // 3. Re-assert our extended styles: connecting the backdrop makes
        // WinUI rewrite GWL_EXSTYLE and our click-through/no-activate/
        // tool-window/layered bits vanish (CI-verified, run 35079479453).
        OverlayNative.EnableLayeredClickThrough(hwnd);
        return true;
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
///   - WM_DWMCOMPOSITIONCHANGED: re-apply the DWM transparency state,
///     because DWM resets it when composition toggles (RDP, driver reset).
///   - WM_PAINT fill was TRIED AND REVERTED (cnbluefire fills WM_PAINT on a
///     non-layered window; on our layered window returning 1 from WM_PAINT
///     correlated with hit-test regression in CI run 35079027989) - default
///     painting is left in place.
/// </summary>
internal static class OverlayWin32Subclass
{
    // Keep the delegate rooted for the window's lifetime (GC must not
    // collect the callback while the native subclass is installed).
    private static OverlayNative.SubclassProc? _proc;

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
            Trace.Log("subclass: installed (WM_DWMCOMPOSITIONCHANGED only)");
        }
    }

    private static IntPtr Handler(
        IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        if (msg == 0x031E) // WM_DWMCOMPOSITIONCHANGED
        {
            int hr = OverlayNative.ApplyTransparentWin32Layer(hwnd);
            OverlayNative.InvalidateRect(hwnd, IntPtr.Zero, true);
            Trace.Log($"subclass: WM_DWMCOMPOSITIONCHANGED -> re-applied (hr={hr})");
        }

        return OverlayNative.DefSubclassProc(hwnd, msg, wParam, lParam);
    }
}
