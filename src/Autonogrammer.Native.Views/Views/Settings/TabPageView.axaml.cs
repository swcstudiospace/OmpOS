using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Tab page: whether inline completion is on, the host and port of its provider (never a full address or a credential) and how it is
/// configured. The page has nothing to edit.
/// </summary>
internal partial class TabPageView : SettingsPageViewBase
{
    /// <summary>Creates a view with no page.</summary>
    public TabPageView()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets the status line.</summary>
    internal TextBlock StatusText => StatusLine;

    /// <summary>Gets the endpoint line.</summary>
    internal SelectableTextBlock Endpoint => EndpointLine;

    /// <summary>Gets the line that says how completion is configured.</summary>
    internal TextBlock HowConfigured => HowConfiguredLine;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;
}
