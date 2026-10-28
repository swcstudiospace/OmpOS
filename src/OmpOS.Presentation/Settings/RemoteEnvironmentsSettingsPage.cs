using OmpOS.Presentation.Remote;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// The Remote section of the settings window: a page that holds the <see cref="RemoteEnvironmentsViewModel"/> its view draws. The view loads the
/// model itself and never starts a connection by itself, so this page is never dirty, never loads again and stays Ready.
/// </summary>
public sealed class RemoteEnvironmentsSettingsPage : SettingsPageBase, IRemoteEnvironmentsModelSource
{
    /// <summary>Initializes a new instance of the <see cref="RemoteEnvironmentsSettingsPage"/> class.</summary>
    /// <param name="model">The model the view draws.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public RemoteEnvironmentsSettingsPage(RemoteEnvironmentsViewModel model)
        : base(SettingsSection.Remote, RemoteCopy.Title, SettingsPageState.Ready)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
    }

    /// <inheritdoc/>
    public RemoteEnvironmentsViewModel Model { get; }

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
