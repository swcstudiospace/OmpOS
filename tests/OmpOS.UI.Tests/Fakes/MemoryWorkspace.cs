using OmpOS.Presentation.Editor;

namespace OmpOS.UI.Tests.Fakes;

/// <summary>A workspace that lives in memory: the files the agent left, so a keep, an undo or a review writes somewhere a test can read.</summary>
internal sealed class MemoryWorkspace : IWorkspaceFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string RootPath => "/workspace";

    /// <summary>Adds or replaces a file.</summary>
    /// <param name="path">The workspace-relative path.</param>
    /// <param name="content">The text of the file.</param>
    /// <returns>The workspace, for chaining.</returns>
    internal MemoryWorkspace With(string path, string content)
    {
        _files[path] = content;
        return this;
    }

    /// <inheritdoc />
    public IReadOnlyList<WorkspaceEntry> ListDirectory(string relativeDirectory) =>
        [.. _files.Keys.Select(static path => new WorkspaceEntry(path[(path.LastIndexOf('/') + 1)..], path, IsDirectory: false))];

    /// <inheritdoc />
    public string ReadFile(string relativePath) =>
        _files.TryGetValue(relativePath, out var text) ? text : throw new FileNotFoundException($"No file exists at '{relativePath}'.");

    /// <inheritdoc />
    public void WriteFile(string relativePath, string content, string expectedCurrent)
    {
        if (!string.Equals(ReadFile(relativePath), expectedCurrent, StringComparison.Ordinal))
        {
            throw new StaleWorkspaceFileException($"'{relativePath}' changed after it was read; nothing was written.");
        }

        _files[relativePath] = content;
    }
}
