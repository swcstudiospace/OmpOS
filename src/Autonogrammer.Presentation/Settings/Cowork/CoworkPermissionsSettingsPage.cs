using Autonogrammer.Presentation.Settings.Permissions;

namespace Autonogrammer.Presentation.Settings.Cowork;

/// <summary>The Cowork permissions page. Auto is labelled Auto (rules).</summary>
public sealed class CoworkPermissionsSettingsPage : SettingsPageBase
{
    /// <summary>Creates the page over folder grants.</summary>
    /// <param name="grants">The grant source.</param>
    public CoworkPermissionsSettingsPage(IFolderGrantSource grants)
        : base(SettingsSection.CoworkPermissions, SettingsCopy.CoworkPermissionsTitle, SettingsPageState.Ready)
    {
        ArgumentNullException.ThrowIfNull(grants);
        Modes = new PermissionModesModel();
        Grants = new FolderGrantModel(grants);
        Statements = new PermissionStatementModel();
    }

    /// <summary>Gets the mode picker.</summary>
    public PermissionModesModel Modes { get; }

    /// <summary>Gets the folder grants.</summary>
    public FolderGrantModel Grants { get; }

    /// <summary>Gets the fixed sentences.</summary>
    public PermissionStatementModel Statements { get; }

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
