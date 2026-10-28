using OmpOS.Presentation.Remote;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// The Remote section of the settings window: a page that holds the <see cref="RemoteEnvironmentsViewModel"/> its view draws and, on a head that can
/// run podman or WSL, the <see cref="RuntimeEnvironmentsViewModel"/> of the Containers and WSL section under it. The views load the models themselves
/// and never start a connection by themselves, so this page is never dirty, never loads again and stays Ready.
/// </summary>
public sealed class RemoteEnvironmentsSettingsPage : SettingsPageBase, IRemoteEnvironmentsModelSource
{
    /// <summary>Initializes a new instance of the <see cref="RemoteEnvironmentsSettingsPage"/> class.</summary>
    /// <param name="model">The model the view draws.</param>
    /// <param name="runtime">The model of the Containers and WSL section, or <see langword="null"/> when the head has none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public RemoteEnvironmentsSettingsPage(RemoteEnvironmentsViewModel model, RuntimeEnvironmentsViewModel? runtime = null)
        : base(SettingsSection.Remote, RemoteCopy.Title, SettingsPageState.Ready)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        Runtime = runtime;
    }

    /// <inheritdoc/>
    public RemoteEnvironmentsViewModel Model { get; }

    /// <summary>Gets the model of the Containers and WSL section, or <see langword="null"/> when the head has none.</summary>
    public RuntimeEnvironmentsViewModel? Runtime { get; }

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
