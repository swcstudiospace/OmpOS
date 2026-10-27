using OmpOS.Workspace.Sets;

namespace OmpOS.Workspace.Tests.Sets;

/// <summary>
/// An in-memory <see cref="IDirectoryProbe"/>: directories, links to directories and git markers are declared by the test, and every
/// question asked is recorded.
/// </summary>
internal sealed class FakeDirectoryProbe : IDirectoryProbe
{
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly HashSet<string> _gitRoots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _links = new(StringComparer.Ordinal);

    /// <summary>Gets every path a question was asked about.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>Normalises a test path the way the validator does.</summary>
    /// <param name="path">The path.</param>
    public static string Full(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

    /// <summary>Declares an existing directory.</summary>
    /// <param name="path">The directory.</param>
    /// <param name="git">Whether it is the top of a git working tree.</param>
    public FakeDirectoryProbe WithDirectory(string path, bool git = true)
    {
        _directories.Add(Full(path));
        if (git)
        {
            _gitRoots.Add(Full(path));
        }

        return this;
    }

    /// <summary>Declares a link that resolves to an existing directory.</summary>
    /// <param name="link">The link path.</param>
    /// <param name="target">The directory it points to.</param>
    public FakeDirectoryProbe WithLink(string link, string target)
    {
        _directories.Add(Full(link));
        _links[Full(link)] = Full(target);
        return this;
    }

    /// <inheritdoc />
    public bool Exists(string path)
    {
        Asked.Add(path);
        return _directories.Contains(Full(path));
    }

    /// <inheritdoc />
    public string? RealPath(string path)
    {
        Asked.Add(path);
        var full = Full(path);
        if (!_directories.Contains(full))
        {
            return null;
        }

        return _links.GetValueOrDefault(full, full);
    }

    /// <inheritdoc />
    public bool IsGitRoot(string path)
    {
        Asked.Add(path);
        return _gitRoots.Contains(Full(path));
    }
}
