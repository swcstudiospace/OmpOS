using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Customize;
using OmpOS.Workspace;

namespace OmpOS.Native.Settings;

/// <summary>Reads the workspace's rule and skill files and maps a folder of them to a <see cref="CustomizeCatalog"/>, the way the Rules and Skills pages list them.</summary>
internal static class WorkspaceSnapshotReader
{
    /// <summary>Loads the convention files of a workspace once.</summary>
    /// <param name="workspaceRoot">The workspace directory.</param>
    /// <returns>The snapshot. Malformed files are its diagnostics.</returns>
    /// <exception cref="SettingsSourceException">The workspace cannot be read.</exception>
    public static WorkspaceSnapshot Read(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        try
        {
            using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspaceRoot));
            return catalog.Current;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or WorkspacePathException)
        {
            throw new SettingsSourceException(HeadSettingsCopy.WorkspaceUnreadable, exception);
        }
    }

    /// <summary>Maps the documents of one convention folder to catalogue entries, with one problem per malformed file of that folder.</summary>
    /// <param name="documents">The rules or the skills of the snapshot.</param>
    /// <param name="snapshot">The snapshot the documents come from.</param>
    /// <param name="folder">The convention folder, relative to the workspace, with <c>/</c> separators.</param>
    /// <returns>The catalogue.</returns>
    public static CustomizeCatalog ToCatalog(IReadOnlyList<WorkspaceDocument> documents, WorkspaceSnapshot snapshot, string folder)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var entries = documents
            .Select(document => new CustomizeEntry(document.Id, CustomizeScope.Project, document.RelativePath, CustomizeDescription.FromMarkdown(document.Text)))
            .ToArray();
        var prefix = folder.TrimEnd('/') + "/";
        var problems = snapshot.Diagnostics
            .Where(diagnostic => diagnostic.RelativePath.StartsWith(prefix, StringComparison.Ordinal))
            .Select(diagnostic => $"{diagnostic.RelativePath}: {diagnostic.Reason}")
            .ToArray();
        return new CustomizeCatalog(entries, problems);
    }
}
