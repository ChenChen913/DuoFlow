using System;
using Microsoft.UI.Composition;   // NOT Windows.UI.Composition: since WinAppSDK
                                  // 1.1 the SystemBackdrop override signature
                                  // (ICompositionSupportsSystemBackdrop) lives in
                                  // Microsoft.UI.Composition. BUT the
                                  // SystemBackdrop PROPERTY on that interface is
                                  // typed as Windows.UI.Composition.CompositionBrush
                                  // (cross-projected), so the brush must be created
                                  // by a Windows.UI.Composition.Compositor -
                                  // exactly the castorix recipe.
using Microsoft.UI.Xaml;
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
/// </summary>
internal sealed class TransparentBackdrop : SystemBackdrop
{
    private Windows.UI.Composition.Compositor? _compositor;

    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        try
        {
            // CI-verified (run 35074034434): "new Compositor()" fails with
            // "Access is denied. The caller must initialize DispatcherQueue
            // on this thread before this operation." - the WinRT composition
            // factory requires a DispatcherQueue even on the XAML UI thread
            // (it is not automatically registered for it this early in
            // window construction). Ensure one exists first, then build the
            // compositor. The brush MUST come from a Windows.UI.Composition
            // Compositor because ICompositionSupportsSystemBackdrop.
            // SystemBackdrop is typed in that namespace (run 35073292924).
            _compositor ??= CreateCompositorWithDispatcherQueue();
            connectedTarget.SystemBackdrop =
                _compositor.CreateColorBrush(Windows.UI.Color.FromArgb(0, 255, 0, 255));
            Trace.Log("backdrop: alpha-0 system backdrop brush connected");
        }
        catch (Exception ex)
        {
            Trace.Log($"backdrop: OnTargetConnected FAILED: {ex.Message}");
            throw;
        }
    }

    private Windows.UI.Composition.Compositor CreateCompositorWithDispatcherQueue()
    {
        // IMPORTANT: the OS composition factory checks the WINDOWS.SYSTEM
        // DispatcherQueue, NOT the Microsoft.UI.Dispatching one that WinUI 3
        // registers on its UI thread (CI-verified twice: with the MSFT queue
        // present, "new Compositor()" still throws Access is denied -
        // run 35074411095). The desktop projection has no managed
        // CreateOnCurrentThread (CS0117, run 35074837054), so the native
        // CoreMessaging export is used (castorix helper).
        if (!OverlayNative.EnsureOsDispatcherQueue())
        {
            throw new InvalidOperationException(
                "Could not ensure an OS DispatcherQueue for Windows.UI.Composition.Compositor.");
        }

        return new Windows.UI.Composition.Compositor();
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        disconnectedTarget.SystemBackdrop = null;
        base.OnTargetDisconnected(disconnectedTarget);
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
