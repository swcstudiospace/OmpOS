using Autonogrammer.Workspace.Ignore;

namespace Autonogrammer.Workspace.Tests.Ignore;

/// <summary>
/// An ignore-file tree held in memory, with workspace-relative <c>/</c> paths. It stands for the disk, so symbolic links and oversize files can be tested without creating them.
/// </summary>
internal sealed class InMemoryIgnoreFileSource : IIgnoreFileSource
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _links = new(StringComparer.Ordinal);

    /// <summary>Gets the paths that were read, in order.</summary>
    public List<string> Read { get; } = [];

    /// <summary>Adds a file.</summary>
    /// <param name="path">The workspace-relative path.</param>
    /// <param name="text">The file text.</param>
    public void Add(string path, string text) => _files[path] = text;

    /// <summary>Adds a file that is a symbolic link: it has content, but <see cref="IsSymbolicLink"/> reports it.</summary>
    /// <param name="path">The workspace-relative path.</param>
    /// <param name="text">The text behind the link.</param>
    public void AddSymbolicLink(string path, string text)
    {
        _files[path] = text;
        _links.Add(path);
    }

    /// <inheritdoc/>
    public string? ReadText(string relativePath, long maxBytes)
    {
        Read.Add(relativePath);
        return _files.GetValueOrDefault(relativePath);
    }

    /// <inheritdoc/>
    public bool IsSymbolicLink(string relativePath) => _links.Contains(relativePath);
}
