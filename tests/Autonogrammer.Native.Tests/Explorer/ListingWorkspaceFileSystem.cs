using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Native.Tests.Explorer;

/// <summary>
/// A workspace file system held in memory, for the explorer tests. It records every directory it is asked to list, so a test
/// can assert when children are loaded, and it lists each directory's children sorted by name.
/// </summary>
internal sealed class ListingWorkspaceFileSystem : IWorkspaceFileSystem
{
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { string.Empty };
    private readonly HashSet<string> _files = new(StringComparer.Ordinal);
    private readonly List<string> _listed = [];

    /// <summary>Creates an empty file system that reports the given root path.</summary>
    /// <param name="rootPath">The absolute root path the file system reports.</param>
    public ListingWorkspaceFileSystem(string rootPath)
    {
        RootPath = rootPath;
    }

    /// <summary>Gets the directories listed so far, in the order they were listed.</summary>
    public IReadOnlyList<string> ListedDirectories => _listed;

    /// <inheritdoc/>
    public string RootPath { get; }

    /// <summary>Adds a file, and any missing parent directories.</summary>
    /// <param name="relativePath">The workspace-relative path of the file.</param>
    /// <returns>This file system, for chaining.</returns>
    public ListingWorkspaceFileSystem WithFile(string relativePath)
    {
        var path = WorkspacePath.Normalize(relativePath);
        AddParents(path);
        _files.Add(path);
        return this;
    }

    /// <summary>Adds a directory, and any missing parent directories.</summary>
    /// <param name="relativePath">The workspace-relative path of the directory.</param>
    /// <returns>This file system, for chaining.</returns>
    public ListingWorkspaceFileSystem WithDirectory(string relativePath)
    {
        var path = WorkspacePath.Normalize(relativePath);
        AddParents(path);
        _directories.Add(path);
        return this;
    }

    /// <inheritdoc/>
    public IReadOnlyList<WorkspaceEntry> ListDirectory(string relativeDirectory)
    {
        _listed.Add(relativeDirectory);
        if (!_directories.Contains(relativeDirectory))
        {
            throw new DirectoryNotFoundException($"No directory exists at '{relativeDirectory}'.");
        }

        var directories = _directories
            .Where(path => path.Length > 0 && string.Equals(ParentOf(path), relativeDirectory, StringComparison.Ordinal))
            .Select(path => new WorkspaceEntry(NameOf(path), path, IsDirectory: true));
        var files = _files
            .Where(path => string.Equals(ParentOf(path), relativeDirectory, StringComparison.Ordinal))
            .Select(path => new WorkspaceEntry(NameOf(path), path, IsDirectory: false));

        return directories.Concat(files).OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray();
    }

    private void AddParents(string path)
    {
        for (var slash = path.IndexOf('/', StringComparison.Ordinal); slash >= 0; slash = path.IndexOf('/', slash + 1))
        {
            _directories.Add(path[..slash]);
        }
    }

    private static string ParentOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }

    private static string NameOf(string path) => path[(path.LastIndexOf('/') + 1)..];
}
