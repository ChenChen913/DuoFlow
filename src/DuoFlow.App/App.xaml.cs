using System;
using System.IO;
using DuoFlow.Camera;
using Microsoft.UI.Xaml;

namespace DuoFlow.App;

/// <summary>
/// DuoFlow application entry point.
/// M0.3: transparent fullscreen topmost click-through overlay (carrying the
/// M0.2 capture chain) + a control console for the verification matrix.
/// </summary>
public partial class App : Application
{
    private OverlayWindow? _overlay;
    private MainWindow? _console;
    private int _swallowCount;

    public App()
    {
        InitializeComponent();

        // M0.3 diagnostics: persist unhandled exceptions so a headless CI run
        // reveals WHY a launch died instead of silently exiting.
        UnhandledException += OnUiUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
    }

    private static string CrashLog
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "duoflow-crash.txt");

    private void OnUiUnhandled(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            File.AppendAllText(CrashLog, $"[UI.Unhandled] {e.Message}\n{e.Exception}\n\n");
        }
        catch
        {
            // Never let the logger itself crash the app.
        }
        Trace.Log($"UI.Unhandled: {e.Message}");

        // Keep the app alive so the console can still produce the smoke JSON;
        // bail out (crash loudly) after 50 swallowed exceptions.
        if (++_swallowCount < 50)
        {
            e.Handled = true;
        }
    }

    private void OnDomainUnhandled(object sender, System.UnhandledExceptionEventArgs e)
    {
        try
        {
            File.AppendAllText(CrashLog, $"[Domain.Unhandled] {e.ExceptionObject}\n\n");
        }
        catch
        {
        }
        Trace.Log($"Domain.Unhandled: {e.ExceptionObject}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Trace.Log("launch begin");

        bool smoke = false;
        bool selfTest = false;
        bool lightSweep = false;
        string? cameraProbeDir = null;
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (string.Equals(arg, "--overlay-smoke", StringComparison.OrdinalIgnoreCase))
            {
                smoke = true;
            }
            if (string.Equals(arg, "--warp-selftest", StringComparison.OrdinalIgnoreCase))
            {
                selfTest = true;
            }
            // M2.2 acceptance hook: seed the sweep ON so warp-selftest can
            // measure the band. Test-only, same category as --warp-selftest.
            if (string.Equals(arg, "--light-sweep", StringComparison.OrdinalIgnoreCase))
            {
                lightSweep = true;
            }
            // M3.1 acceptance hook. Takes an optional output directory:
            //   --camera-probe                -> %TEMP%\duoflow-camera-probe
            //   --camera-probe=D:\tmp\cam     -> that directory
            if (arg.StartsWith("--camera-probe", StringComparison.OrdinalIgnoreCase))
            {
                int eq = arg.IndexOf('=');
                cameraProbeDir = eq > 0
                    ? arg[(eq + 1)..]
                    : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "duoflow-camera-probe");
            }
        }
        Trace.Log($"smoke={smoke} selfTest={selfTest} lightSweep={lightSweep} cameraProbe={cameraProbeDir ?? "-"}");

        // The camera probe is a headless mode: no overlay, no console, no
        // rendering. It measures one thing (can this machine deliver frames)
        // and exits, so it can run beside anything else on the desktop.
        if (cameraProbeDir is not null)
        {
            RunCameraProbeThenExit(cameraProbeDir);
            return;
        }

        try
        {
            Trace.Log("overlay: constructing...");
            _overlay = new OverlayWindow(selfTest);
            Trace.Log("overlay: constructed OK");
            _overlay.Activate();
            Trace.Log("overlay: activated OK");
        }
        catch (Exception ex)
        {
            Trace.Log($"overlay: ctor/activate FAILED: {ex.Message}");
            try { File.AppendAllText(CrashLog, $"[Overlay ctor] {ex}\n\n"); } catch { }
            _overlay = null; // console reports OverlayCreated=false
        }

        try
        {
            Trace.Log("console: constructing...");
            // automation: the three test hooks below change UI state on
            // purpose; DD-051 keeps those runs hermetic (no preset read/write).
            _console = new MainWindow(_overlay, smoke, lightSweep, smoke || selfTest || lightSweep);
            Trace.Log("console: constructed OK");
            _console.Activate();
            Trace.Log("console: activated OK");
        }
        catch (Exception ex)
        {
            Trace.Log($"console: ctor/activate FAILED: {ex.Message}");
            try { File.AppendAllText(CrashLog, $"[Console ctor] {ex}\n\n"); } catch { }
        }

        Trace.Log("launch end");
    }

    /// <summary>
    /// Run the M3.1 probe and terminate the process with its exit code.
    /// The probe awaits WinRT I/O, so it cannot run on the UI thread; the exit
    /// itself must be posted back to it (WinUI tears the app down from there).
    /// </summary>
    private void RunCameraProbeThenExit(string outputDir)
    {
        Microsoft.UI.Dispatching.DispatcherQueue dispatcher =
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        _ = Task.Run(async () =>
        {
            int exitCode = 1;
            try
            {
                exitCode = await CameraProbe.RunAsync(outputDir, MediaCaptureCamera.DefaultTargetFps, 30);
            }
            catch (Exception ex)
            {
                Trace.Log($"camera-probe: unhandled {ex.GetType().Name}: {ex.Message}");
            }

            dispatcher.TryEnqueue(() =>
            {
                Environment.ExitCode = exitCode;
                Exit();
            });
        });
    }
}
