using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Worktrees;

namespace OmpOS.Native.Tests.Settings;

/// <summary>A scripted source of the Worktrees page: it reports the snapshot it holds and counts the loads.</summary>
internal sealed class FakeWorktreesSource : IWorktreesSettingsSource
{
    /// <summary>Gets or sets the snapshot the source reports.</summary>
    public WorktreesSnapshot Snapshot { get; set; } = new("/work/app", 25, WorktreeSetupState.NoSetupFile, string.Empty, [], string.Empty, string.Empty);

    /// <summary>Gets how many loads were asked for.</summary>
    public int LoadCount { get; private set; }

    /// <inheritdoc />
    public Task<WorktreesSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCount++;
        return Task.FromResult(Snapshot);
    }
}
