using Autonogrammer.Presentation.Usage;

namespace Autonogrammer.Presentation.Settings.Cowork;

/// <summary>The usage page. It has no plan limits and no billing.</summary>
public sealed class UsageSettingsPage : SettingsPageBase
{
    /// <summary>Creates the page over a meter.</summary>
    /// <param name="model">The meter. Null is empty.</param>
    public UsageSettingsPage(UsageMeterModel? model = null)
        : base(SettingsSection.Usage, SettingsCopy.UsageTitle, SettingsPageState.Ready)
    {
        Model = model ?? UsageMeterModel.Empty;
    }

    /// <summary>Gets the meter the view binds.</summary>
    public UsageMeterModel Model { get; }

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
