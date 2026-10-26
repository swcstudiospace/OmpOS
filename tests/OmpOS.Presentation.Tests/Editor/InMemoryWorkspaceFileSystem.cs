using OmpOS.Presentation.Editor;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// A workspace file system held in memory, for tests of <see cref="WorkspaceTree"/>. It records every directory it is asked to
/// list, so a test can assert when children are loaded.
/// </summary>
internal sealed class InMemoryWorkspaceFileSystem : IWorkspaceFileSystem
{
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { string.Empty };
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly List<string> _listed = [];

    /// <summary>Creates an empty file system with the given root path label.</summary>
    /// <param name="rootPath">The root path the file system reports.</param>
    public InMemoryWorkspaceFileSystem(string rootPath = "/workspace")
    {
        RootPath = rootPath;
    }

    /// <summary>Gets the directories listed so far, in the order they were listed.</summary>
    public IReadOnlyList<string> ListedDirectories => _listed;

    /// <summary>Gets the number of writes that reached a file. A refused write does not count.</summary>
    public int WriteCount { get; private set; }

    /// <inheritdoc/>
    public string RootPath { get; }

    /// <summary>Adds a file, and any missing parent directories.</summary>
    /// <param name="relativePath">The workspace-relative path of the file.</param>
    /// <param name="content">The text of the file.</param>
    /// <returns>This file system, for chaining.</returns>
    public InMemoryWorkspaceFileSystem WithFile(string relativePath, string content = "")
    {
        var path = WorkspacePath.Normalize(relativePath);
        AddParents(path);
        _files[path] = content;
        return this;
    }

    /// <summary>Adds a directory, and any missing parent directories.</summary>
    /// <param name="relativePath">The workspace-relative path of the directory.</param>
    /// <returns>This file system, for chaining.</returns>
    public InMemoryWorkspaceFileSystem WithDirectory(string relativePath)
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

        var entries = new List<WorkspaceEntry>();
        foreach (var directory in _directories.Where(path => path.Length > 0 && string.Equals(ParentOf(path), relativeDirectory, StringComparison.Ordinal)))
        {
            entries.Add(new WorkspaceEntry(NameOf(directory), directory, IsDirectory: true));
        }

        foreach (var file in _files.Keys.Where(path => string.Equals(ParentOf(path), relativeDirectory, StringComparison.Ordinal)))
        {
            entries.Add(new WorkspaceEntry(NameOf(file), file, IsDirectory: false));
        }

        return entries;
    }

    /// <inheritdoc/>
    public string ReadFile(string relativePath)
    {
        var path = WorkspacePath.Normalize(relativePath);
        return _files.TryGetValue(path, out var content)
            ? content
            : throw new FileNotFoundException($"No file exists at '{relativePath}'.");
    }

    /// <inheritdoc/>
    public void WriteFile(string relativePath, string content, string expectedCurrent)
    {
        var path = WorkspacePath.Normalize(relativePath);
        if (!_files.TryGetValue(path, out var current))
        {
            throw new FileNotFoundException($"No file exists at '{relativePath}'.");
        }

        if (!string.Equals(current, expectedCurrent, StringComparison.Ordinal))
        {
            throw new StaleWorkspaceFileException($"'{relativePath}' changed after it was read; nothing was written.");
        }

        _files[path] = content;
        WriteCount++;
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
