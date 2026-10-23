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
        string? cameraDetectDir = null;
        string? cameraLidDir = null;
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

            // M3.2 acceptance hook: same shape, but it also runs screen
            // detection on every frame. Only the quad and the confidence leave
            // the callback - still no pixel, still nothing on disk.
            if (arg.StartsWith("--camera-detect", StringComparison.OrdinalIgnoreCase))
            {
                int eq = arg.IndexOf('=');
                cameraDetectDir = eq > 0
                    ? arg[(eq + 1)..]
                    : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "duoflow-camera-detect");
            }
            // M3.3 acceptance hook: the real provider, end to end - coverage
            // estimate, progress mapping, LidState. Same privacy rule: only
            // scalars leave the frame callback.
            if (arg.StartsWith("--camera-lid", StringComparison.OrdinalIgnoreCase))
            {
                int eq = arg.IndexOf('=');
                cameraLidDir = eq > 0
                    ? arg[(eq + 1)..]
                    : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "duoflow-camera-lid");
            }
        }

        Trace.Log($"smoke={smoke} selfTest={selfTest} lightSweep={lightSweep} " +
                  $"cameraProbe={cameraProbeDir ?? "-"} cameraDetect={cameraDetectDir ?? "-"} " +
                  $"cameraLid={cameraLidDir ?? "-"}");

        // The camera probe is a headless mode: no overlay, no console, no
        // rendering. It measures one thing (can this machine deliver frames)
        // and exits, so it can run beside anything else on the desktop.
        if (cameraProbeDir is not null)
        {
            RunHeadlessThenExit("camera-probe", dir =>
                CameraProbe.RunAsync(dir, MediaCaptureCamera.DefaultTargetFps, 30), cameraProbeDir);
            return;
        }

        // M3.2: same headless contract, plus detection. Kept separate from the
        // probe so a detection failure can never make "can this machine open
        // its camera" look broken - they answer different questions.
        if (cameraDetectDir is not null)
        {
            RunHeadlessThenExit("camera-detect", dir =>
                CameraProbe.RunDetectAsync(dir, MediaCaptureCamera.DefaultTargetFps, 30), cameraDetectDir);
            return;
        }

        // M3.3: the provider itself, not a re-implementation of it. If this
        // passes, the path the app will ship is the path that was measured.
        if (cameraLidDir is not null)
        {
            RunHeadlessThenExit("camera-lid", dir =>
                CameraProbe.RunLidAsync(dir, MediaCaptureCamera.DefaultTargetFps, 30), cameraLidDir);
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
    /// <summary>
    /// Runs a headless acceptance routine and exits with its verdict.
    ///
    /// The probe awaits WinRT I/O, so it cannot run on the UI thread; the exit
    /// itself must be posted back to it (WinUI tears the app down from there).
    /// Shared by every headless mode so they cannot drift apart in how they
    /// report success - one of them quietly returning 0 on failure would make
    /// the acceptance scripts lie.
    /// </summary>
    private void RunHeadlessThenExit(string label, Func<string, Task<int>> run, string outputDir)
    {
        Microsoft.UI.Dispatching.DispatcherQueue dispatcher =
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        _ = Task.Run(async () =>
        {
            int exitCode = 1;
            try
            {
                exitCode = await run(outputDir);
            }
            catch (Exception ex)
            {
                Trace.Log($"{label}: unhandled {ex.GetType().Name}: {ex.Message}");
            }

            dispatcher.TryEnqueue(() =>
            {
                Environment.ExitCode = exitCode;
                Exit();
            });
        });
    }
}
