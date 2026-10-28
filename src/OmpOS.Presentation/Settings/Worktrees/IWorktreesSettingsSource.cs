namespace OmpOS.Presentation.Settings.Worktrees;

/// <summary>
/// Where the Worktrees page reads its facts. The page is read-only, so there is no write. A setup file that is invalid is part of the
/// snapshot, not a failure; <see cref="SettingsSourceException"/> is for what cannot be read at all.
/// </summary>
public interface IWorktreesSettingsSource
{
    /// <summary>Reads the worktree facts of the workspace.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="SettingsSourceException">The facts cannot be read.</exception>
    Task<WorktreesSnapshot> LoadAsync(CancellationToken cancellationToken);
}
