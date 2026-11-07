using Autonogrammer.Presentation.Isolation;

namespace Autonogrammer.Presentation.Settings.Cowork;

/// <summary>The isolation page. Network is unrestricted. Terminals, hooks, remote sessions and Check connection runs are not confined.</summary>
public sealed class IsolationSettingsPage : SettingsPageBase
{
    /// <summary>Creates the page over a chip.</summary>
    /// <param name="chip">The chip. Null starts at None with no consent question.</param>
    public IsolationSettingsPage(IsolationChipModel? chip = null)
        : base(SettingsSection.Isolation, SettingsCopy.IsolationTitle, SettingsPageState.Ready)
    {
        Model = chip ?? new IsolationChipModel(nameof(IsolationLevelPlaceholder.None), null, false);
    }

    /// <summary>Gets the chip the view binds.</summary>
    public IsolationChipModel Model { get; }

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private enum IsolationLevelPlaceholder
    {
        None = 0,
    }
}
