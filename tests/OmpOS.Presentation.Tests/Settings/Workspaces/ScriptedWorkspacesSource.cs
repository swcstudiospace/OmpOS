using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Workspaces;

namespace OmpOS.Presentation.Tests.Settings.Workspaces;

/// <summary>A Workspaces source the test scripts: it keeps recents in memory, counts calls and can fail a read, a write or a forget.</summary>
internal sealed class ScriptedWorkspacesSource : IWorkspacesSettingsSource
{
    /// <summary>Gets the backend the folder editor uses.</summary>
    internal ScriptedWorkspacesPickerBackend Backend { get; } = new();

    /// <summary>Gets the recent sets the source holds, newest first.</summary>
    internal List<WorkspaceRecentRow> Recents { get; } = [];

    /// <summary>Gets or sets the reason reads fail with, or null.</summary>
    internal string? ListFailure { get; set; }

    /// <summary>Gets or sets the reason saves fail with, or null.</summary>
    internal string? SaveFailure { get; set; }

    /// <summary>Gets or sets the reason forgets fail with, or null.</summary>
    internal string? ForgetFailure { get; set; }

    /// <summary>Gets or sets whether the list stops being readable after a save, as a store that lost its file would.</summary>
    internal bool ListFailsAfterSave { get; set; }

    /// <summary>Gets the names the source was asked to save, in order.</summary>
    internal List<string> SavedNames { get; } = [];

    /// <summary>Gets the roots of the last save.</summary>
    internal IReadOnlyList<WorkspacePickerRoot> SavedRoots { get; private set; } = [];

    /// <inheritdoc/>
    public IWorkspaceSetPickerBackend PickerBackend => Backend;

    /// <summary>Builds a recent row with one folder per path; the first is the primary.</summary>
    /// <param name="name">The name of the set.</param>
    /// <param name="paths">The folder paths.</param>
    /// <returns>The row; its identifier is the lower-case name.</returns>
    internal static WorkspaceRecentRow Row(string name, params string[] paths) => new(
        name.ToLowerInvariant(),
        name,
        paths.Length,
        paths[0],
        "2026-10-28 09:00 UTC",
        [.. paths.Select((path, index) => new WorkspacePickerRoot("r" + index, path, "r" + index, null, index == 0))]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkspaceRecentRow>> ListRecentsAsync(CancellationToken cancellationToken)
    {
        if (ListFailure is not null || (ListFailsAfterSave && SavedNames.Count > 0))
        {
            return Task.FromException<IReadOnlyList<WorkspaceRecentRow>>(new SettingsSourceException(ListFailure ?? "The list is gone."));
        }

        return Task.FromResult<IReadOnlyList<WorkspaceRecentRow>>([.. Recents]);
    }

    /// <inheritdoc/>
    public Task SaveAsync(string name, IReadOnlyList<WorkspacePickerRoot> roots, CancellationToken cancellationToken)
    {
        if (SaveFailure is { } failure)
        {
            return Task.FromException(new SettingsSourceException(failure));
        }

        SavedNames.Add(name);
        SavedRoots = roots;
        Recents.RemoveAll(row => string.Equals(row.Id, name.ToLowerInvariant(), StringComparison.Ordinal));
        Recents.Insert(0, new WorkspaceRecentRow(name.ToLowerInvariant(), name, roots.Count, roots[0].Path, "2026-10-28 10:00 UTC", roots));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ForgetAsync(string setId, CancellationToken cancellationToken)
    {
        if (ForgetFailure is { } failure)
        {
            return Task.FromException<bool>(new SettingsSourceException(failure));
        }

        return Task.FromResult(Recents.RemoveAll(row => string.Equals(row.Id, setId, StringComparison.Ordinal)) > 0);
    }
}
