using OmpOS.Presentation.Cloud;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// The Cloud section of the settings window: a page that holds the <see cref="CloudAgentsViewModel"/> its view draws. The view loads the model
/// itself when it gets it, so this page never loads again; it stays Ready so no loading or failure state hides the view's own states (the
/// not-configured state included).
/// </summary>
public sealed class CloudSettingsPage : SettingsPageBase
{
    /// <summary>Initializes a new instance of the <see cref="CloudSettingsPage"/> class.</summary>
    /// <param name="agents">The model the view draws.</param>
    /// <exception cref="ArgumentNullException"><paramref name="agents"/> is null.</exception>
    public CloudSettingsPage(CloudAgentsViewModel agents)
        : base(SettingsSection.Cloud, CloudCopy.Title, SettingsPageState.Ready)
    {
        ArgumentNullException.ThrowIfNull(agents);
        Agents = agents;
    }

    /// <summary>Gets the model the view draws.</summary>
    public CloudAgentsViewModel Agents { get; }

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
