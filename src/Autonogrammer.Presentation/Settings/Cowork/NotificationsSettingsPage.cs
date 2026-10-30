namespace Autonogrammer.Presentation.Settings.Cowork;

/// <summary>
/// The notifications page. OS text is title-only by default. The in-app centre is a pane, not this section.
/// Scheduled-task kinds have no producer until those events exist.
/// </summary>
public sealed class NotificationsSettingsPage : SettingsPageBase
{
    /// <summary>Creates the page.</summary>
    public NotificationsSettingsPage()
        : base(SettingsSection.Notifications, SettingsCopy.NotificationsTitle, SettingsPageState.Ready)
    {
    }

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
