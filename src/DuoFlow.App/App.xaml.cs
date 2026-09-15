using System;
using System.IO;
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
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        bool smoke = false;
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (string.Equals(arg, "--overlay-smoke", StringComparison.OrdinalIgnoreCase))
            {
                smoke = true;
            }
        }

        try
        {
            _overlay = new OverlayWindow();
            _overlay.Activate();
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(CrashLog, $"[Overlay ctor] {ex}\n\n"); } catch { }
            _overlay = null; // console reports OverlayCreated=false
        }

        try
        {
            _console = new MainWindow(_overlay, smoke);
            _console.Activate();
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(CrashLog, $"[Console ctor] {ex}\n\n"); } catch { }
        }
    }
}
