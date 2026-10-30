using Autonogrammer.Presentation.Automations;

namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// The Automations section of the settings window: a page that holds the <see cref="HostedAutomationsViewModel"/> its view draws. The view loads the
/// model itself when it gets it, so this page never loads again; it stays Ready so no loading or failure state hides the view's own states.
/// </summary>
public sealed class AutomationsSettingsPage : SettingsPageBase
{
    /// <summary>Initializes a new instance of the <see cref="AutomationsSettingsPage"/> class.</summary>
    /// <param name="model">The model the view draws.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public AutomationsSettingsPage(HostedAutomationsViewModel model)
        : base(SettingsSection.Automations, AutomationsCopy.Title, SettingsPageState.Ready)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
    }

    /// <summary>Gets the model the view draws.</summary>
    public HostedAutomationsViewModel Model { get; }

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
