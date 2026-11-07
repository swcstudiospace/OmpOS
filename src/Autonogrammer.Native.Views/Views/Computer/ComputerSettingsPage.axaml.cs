using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Computer;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Computer;

/// <summary>The Computer Use settings page view.</summary>
internal partial class ComputerSettingsPage : SettingsPageViewBase
{
    /// <summary>Creates the view.</summary>
    public ComputerSettingsPage()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets Open System Settings.</summary>
    internal Button OpenSettings => OpenSettingsButton;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    private void OnOptIn(object? sender, RoutedEventArgs e) => (DataContext as ComputerUseSettingsPage)?.Model.OptIn.Accept();

    private void OnDecline(object? sender, RoutedEventArgs e) => (DataContext as ComputerUseSettingsPage)?.Model.OptIn.Decline();
}
