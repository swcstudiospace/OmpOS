using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Settings.Workspaces;

namespace OmpOS.Native.Tests.Settings;

/// <summary>A Workspaces source for the view tests: recents in memory, and a folder check that fails a path containing "missing".</summary>
internal sealed class FakeWorkspacesSource : IWorkspacesSettingsSource
{
    private readonly Backend _backend = new();

    /// <summary>Gets the recent sets the source holds, newest first.</summary>
    internal List<WorkspaceRecentRow> Recents { get; } = [];

    /// <inheritdoc/>
    public IWorkspaceSetPickerBackend PickerBackend => _backend;

    /// <summary>Builds a recent row with one folder per path; the first is the primary.</summary>
    /// <param name="name">The name of the set.</param>
    /// <param name="paths">The folder paths.</param>
    /// <returns>The row.</returns>
    internal static WorkspaceRecentRow Row(string name, params string[] paths) => new(
        name.ToLowerInvariant(),
        name,
        paths.Length,
        paths[0],
        "2026-10-28 09:00 UTC",
        [.. paths.Select((path, index) => new WorkspacePickerRoot("r" + index, path, "r" + index, null, index == 0))]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkspaceRecentRow>> ListRecentsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkspaceRecentRow>>([.. Recents]);

    /// <inheritdoc/>
    public Task SaveAsync(string name, IReadOnlyList<WorkspacePickerRoot> roots, CancellationToken cancellationToken)
    {
        Recents.RemoveAll(row => string.Equals(row.Name, name, StringComparison.Ordinal));
        Recents.Insert(0, new WorkspaceRecentRow(name.ToLowerInvariant(), name, roots.Count, roots[0].Path, "2026-10-28 10:00 UTC", roots));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ForgetAsync(string setId, CancellationToken cancellationToken) =>
        Task.FromResult(Recents.RemoveAll(row => string.Equals(row.Id, setId, StringComparison.Ordinal)) > 0);

    private sealed class Backend : IWorkspaceSetPickerBackend
    {
        public IReadOnlyList<WorkspacePickerIssue> Validate(IReadOnlyList<WorkspacePickerRoot> roots) =>
            [.. roots
                .Where(static root => root.Path.Contains("missing", StringComparison.Ordinal))
                .Select(static root => new WorkspacePickerIssue(IsError: true, root.Id, $"'{root.DisplayName}': the folder '{root.Path}' does not exist."))];

        public WorkspacePickerImport ImportCodeWorkspace(string filePath) => throw new FileNotFoundException("No such file.", filePath);

        public void ExportCodeWorkspace(string filePath, IReadOnlyList<WorkspacePickerRoot> roots)
        {
        }
    }
}
