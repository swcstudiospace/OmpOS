using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The plugin-bundle section. It binds the Presentation models only. Wiring into the settings window belongs to a later plan.
/// </summary>
public partial class BundlesPageView : UserControl
{
    /// <summary>Creates a view with no section.</summary>
    public BundlesPageView()
    {
        InitializeComponent();
    }
}
