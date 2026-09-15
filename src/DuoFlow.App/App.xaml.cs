using System;
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

    public App()
    {
        InitializeComponent();
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
        catch
        {
            // The console reports OverlayCreated=false; never crash the launch.
            _overlay = null;
        }

        _console = new MainWindow(_overlay, smoke);
        _console.Activate();
    }
}
