using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Worktrees;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>A Worktrees source the test scripts: it reports the snapshot it holds, counts loads, or fails them.</summary>
internal sealed class ScriptedWorktreesSource : IWorktreesSettingsSource
{
    /// <summary>Gets or sets the snapshot the source reports.</summary>
    internal WorktreesSnapshot Snapshot { get; set; } = new("/work/app", 25, WorktreeSetupState.NoSetupFile, string.Empty, [], string.Empty, string.Empty);

    /// <summary>Gets or sets the reason loads fail with, or null when they succeed.</summary>
    internal string? LoadFailure { get; set; }

    /// <summary>Gets how many loads were asked for.</summary>
    internal int LoadCalls { get; private set; }

    /// <inheritdoc/>
    public Task<WorktreesSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCalls++;
        return LoadFailure is { } failure
            ? Task.FromException<WorktreesSnapshot>(new SettingsSourceException(failure))
            : Task.FromResult(Snapshot);
    }
}
