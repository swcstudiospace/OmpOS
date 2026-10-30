using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Customize;

namespace Autonogrammer.Presentation.Tests.Settings.Customize;

/// <summary>One scripted catalogue source that serves the Rules, Skills and Commands pages alike.</summary>
internal sealed class ScriptedCustomizeSource : IRulesSettingsSource, ISkillsSettingsSource, ICommandsSettingsSource
{
    /// <summary>Gets or sets the catalogue the source returns.</summary>
    public CustomizeCatalog Catalog { get; set; } = new([], []);

    /// <summary>Gets or sets the message a load fails with; null lets loads succeed.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>Gets or sets a gate a load waits for.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets the number of loads.</summary>
    public int LoadCalls { get; private set; }

    /// <inheritdoc/>
    public async Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCalls++;
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        return FailureMessage is null ? Catalog : throw new SettingsSourceException(FailureMessage);
    }
}
