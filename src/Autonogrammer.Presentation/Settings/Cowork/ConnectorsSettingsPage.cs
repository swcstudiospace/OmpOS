using Autonogrammer.Presentation.Settings.Connectors;

namespace Autonogrammer.Presentation.Settings.Cowork;

/// <summary>The connectors page. Auto does not trust readOnlyHint.</summary>
public sealed class ConnectorsSettingsPage : SettingsPageBase
{
    /// <summary>Creates the page over a permission store.</summary>
    /// <param name="store">The three-state matrix store.</param>
    public ConnectorsSettingsPage(IConnectorPermissionStore store)
        : base(SettingsSection.Connectors, SettingsCopy.ConnectorsTitle, SettingsPageState.Ready)
    {
        Model = new ConnectorMatrixModel(store);
    }

    /// <summary>Gets the matrix the view reads.</summary>
    public ConnectorMatrixModel Model { get; }

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
