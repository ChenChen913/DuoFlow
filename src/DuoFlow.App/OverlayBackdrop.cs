using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;  // ElementCompositionPreview (NOT .Media - CS0103)
using Microsoft.UI.Xaml.Media;

namespace DuoFlow.App;

/// <summary>
/// P0-1 fix (real-machine finding, 2026-09-16): a WinUI 3 window has TWO
/// background layers - the Win32 window background and the
/// DesktopWindowXamlSource Visual background. DwmExtendFrameIntoClientArea
/// only handles the first one; without a fully transparent system-backdrop
/// brush the Visual layer stays opaque black, so the "transparent" overlay
/// hid the entire desktop (sampled luminance 0.0~3.5 under the topmost
/// overlay vs 254.7 with the overlay moved away).
///
/// Setting an alpha=0 brush on ICompositionSupportsSystemBackdrop is what
/// actually removes the black base. There is NO built-in
/// "TransparentBackdrop" class in WinAppSDK 1.8 (verified against
/// Microsoft.UI.Xaml.winmd: only SystemBackdrop / MicaBackdrop /
/// DesktopAcrylicBackdrop exist), so this subclasses SystemBackdrop
/// directly - same recipe as castorix/WinUI3_SwapChainPanel_Layered.
///
/// ⚠ Known sharp edge (microsoft-ui-xaml#1247): "layered + SwapChainPanel +
/// transparent" has a long-standing black/white-base issue since 1.1. The
/// overlay is exactly that combination, so transparency, click-through AND
/// the capture preview must be re-verified TOGETHER (CI luminance/hit-test
/// probes + real-machine visual pass).
///
/// Projection traps found along the way (all CI-verified):
///  - the OnTargetConnected override signature uses
///    Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop (CS0115
///    with the Windows.UI.Composition one, run 35073292924);
///  - the SystemBackdrop PROPERTY on that interface is typed as
///    Windows.UI.Composition.CompositionBrush (CS0029, same run);
///  - "new Windows.UI.Composition.Compositor()" requires an OS-level
///    (Windows.System) DispatcherQueue - the WinUI 3 UI thread only has the
///    Microsoft.UI.Dispatching one (Access is denied, runs 35074034434 /
///    35074411095); the managed CreateOnCurrentThread does not exist in the
///    desktop projection (CS0117, run 35074837054) so the native CoreMessaging
///    export is used as fallback.
/// </summary>
internal sealed class TransparentBackdrop : SystemBackdrop
{
    protected override void OnTargetConnected(
        Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        try
        {
            Windows.UI.Composition.CompositionBrush brush = CreateAlphaZeroBrush(xamlRoot);
            connectedTarget.SystemBackdrop = brush;
            Trace.Log("backdrop: alpha-0 system backdrop brush connected");
        }
        catch (Exception ex)
        {
            Trace.Log($"backdrop: OnTargetConnected FAILED: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    protected override void OnTargetDisconnected(
        Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        disconnectedTarget.SystemBackdrop = null;
        base.OnTargetDisconnected(disconnectedTarget);
    }

    /// <summary>
    /// Builds the alpha-0 brush in the WINDOWS.UI.Composition namespace (the
    /// ICompositionSupportsSystemBackdrop.SystemBackdrop property type).
    /// Path 1 (preferred): reuse the XAML visual's own compositor - it is the
    /// one that paints the island, and if Microsoft.UI.Composition.* is a
    /// re-projection of the same WinRT runtime classes the runtime cast to
    /// Windows.UI.Composition.Compositor succeeds.
    /// Path 2 (fallback): ensure an OS-level DispatcherQueue via the native
    /// CoreMessaging export, then construct a fresh Windows.UI.Composition
    /// Compositor (castorix recipe).
    /// Every step is logged so a CI run pinpoints the failing path.
    /// </summary>
    private static Windows.UI.Composition.CompositionBrush CreateAlphaZeroBrush(XamlRoot xamlRoot)
    {
        var alphaZero = Windows.UI.Color.FromArgb(0, 255, 0, 255);

        // ---- Path 1: XAML's own compositor, re-wrapped into the OS projection.
        // A direct C# cast fails (InvalidCastException, run 35075895577):
        // Microsoft.UI.Composition.Compositor and Windows.UI.Composition.Compositor
        // are distinct .NET projection types even when they wrap the same native
        // WinRT object. CsWinRT's As<T>() re-queries the interfaces and wraps the
        // SAME native object in the requested projection.
        try
        {
            Microsoft.UI.Composition.Visual visual =
                ElementCompositionPreview.GetElementVisual((UIElement)xamlRoot.Content);
            // WinRT.MarshalExtensions is internal (CS0122); CastExtensions.As<T>
            // is the public CsWinRT re-wrap entry.
            var osCompositor = WinRT.CastExtensions.As<Windows.UI.Composition.Compositor>(visual.Compositor);
            Trace.Log("backdrop: path1 OK - XAML compositor re-wrapped as Windows.UI.Composition.Compositor");
            return osCompositor.CreateColorBrush(alphaZero);
        }
        catch (Exception ex)
        {
            Trace.Log($"backdrop: path1 (XAML compositor rewrap) failed: {ex.GetType().Name}: {ex.Message}");
        }

        // ---- Path 2: OS DispatcherQueue (native CoreMessaging) + new Compositor.
        if (!OverlayNative.EnsureOsDispatcherQueue())
        {
            throw new InvalidOperationException(
                "Could not ensure an OS DispatcherQueue for Windows.UI.Composition.Compositor.");
        }
        Trace.Log("backdrop: path2 OS DispatcherQueue present, constructing Compositor");
        return new Windows.UI.Composition.Compositor().CreateColorBrush(alphaZero);
    }
}

/// <summary>
/// Win32 message subclass for the overlay top-level window (P0-1 Win32
/// layer, castorix recipe):
///   - WM_ERASEBKGND: fill black and report handled. With the alpha-0
///     backdrop + empty blur region this black is composited as fully
///     transparent (premultiplied alpha), never visible on screen.
///   - WM_DWMCOMPOSITIONCHANGED: re-apply the DWM transparency state (frame
///     extension + blur-behind), because DWM resets it when composition
///     toggles (e.g. RDP sessions, GPU driver resets).
/// </summary>
internal static class OverlayWin32Subclass
{
    private const uint WM_ERASEBKGND = 0x000F;
    private const uint WM_DWMCOMPOSITIONCHANGED = 0x031E;
    private const int BLACK_BRUSH = 4; // GetStockObject index

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
            Trace.Log("subclass: installed (WM_ERASEBKGND / WM_DWMCOMPOSITIONCHANGED)");
        }
    }

    private static IntPtr Handler(
        IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        if (msg == WM_ERASEBKGND)
        {
            if (_blackBrush == IntPtr.Zero)
            {
                _blackBrush = OverlayNative.GetStockObject(BLACK_BRUSH);
            }

            if (OverlayNative.GetClientRect(hwnd, out OverlayNative.RECT rc)
                && OverlayNative.FillRect(wParam, ref rc, _blackBrush) != 0)
            {
                return new IntPtr(1); // background erased (handled)
            }

            return new IntPtr(1); // still report handled: default erase would paint the class brush
        }

        if (msg == WM_DWMCOMPOSITIONCHANGED)
        {
            int hr = OverlayNative.ApplyTransparentWin32Layer(hwnd);
            OverlayNative.InvalidateRect(hwnd, IntPtr.Zero, true);
            Trace.Log($"subclass: WM_DWMCOMPOSITIONCHANGED -> re-applied (hr={hr})");
        }

        return OverlayNative.DefSubclassProc(hwnd, msg, wParam, lParam);
    }
}
