using Microsoft.UI.Xaml;

namespace DuoFlow.App;

/// <summary>
/// M0.1 verification window: proves XAML compilation and runtime bootstrapping work.
/// Rendering experiments (Overlay / Warp) start in M0.2 / M1.4.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
