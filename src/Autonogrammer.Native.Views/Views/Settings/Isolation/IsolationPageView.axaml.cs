using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The isolation settings page. It binds <see cref="Autonogrammer.Presentation.Isolation.IsolationChipModel"/> only.
/// Wiring into the settings window belongs to a later plan.
/// </summary>
public partial class IsolationPageView : UserControl
{
    /// <summary>Creates a view with no model.</summary>
    public IsolationPageView()
    {
        InitializeComponent();
    }
}
