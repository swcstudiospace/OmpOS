using OmpOS.Presentation.Safety;
using OmpOS.Presentation.Settings;

namespace OmpOS.Presentation.Tests.Safety;

/// <summary>A Run mode source that returns a scripted snapshot, records every save, and can fail on demand.</summary>
internal sealed class ScriptedRunModeSource : IRunModeSource
{
    private RunModeSnapshot _snapshot = new()
    {
        Values = new RunModeValues(RunModeChoice.AutoReview, ReadBoundaryChoice.System, ["git status"], [], []),
        EditPermissionsPath = "/w/.ompos/permissions.json",
        EditSandboxPath = "/w/.ompos/sandbox.json",
        OtherTerminalAllowlist = [new RunModeEntry("npm", "/home/u/.ompos/permissions.json", IsEditable: false)],
        ProjectTrusted = true,
    };

    /// <summary>Gets or sets the snapshot the next load returns.</summary>
    public RunModeSnapshot Snapshot
    {
        get => _snapshot;
        set => _snapshot = value;
    }

    /// <summary>Gets or sets a reason that makes a load fail, or null.</summary>
    public string? LoadFailure { get; set; }

    /// <summary>Gets or sets a reason that makes a save fail, or null.</summary>
    public string? SaveFailure { get; set; }

    /// <summary>Gets how many times the source was loaded.</summary>
    public int LoadCalls { get; private set; }

    /// <summary>Gets every set of values saved, in order.</summary>
    public List<RunModeValues> Saved { get; } = [];

    /// <inheritdoc/>
    public Task<RunModeSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCalls++;
        if (LoadFailure is not null)
        {
            throw new SettingsSourceException(LoadFailure);
        }

        return Task.FromResult(_snapshot);
    }

    /// <inheritdoc/>
    public Task SaveAsync(RunModeValues values, CancellationToken cancellationToken)
    {
        if (SaveFailure is not null)
        {
            throw new SettingsSourceException(SaveFailure);
        }

        Saved.Add(values);
        _snapshot = _snapshot with { Values = values };
        return Task.CompletedTask;
    }
}
