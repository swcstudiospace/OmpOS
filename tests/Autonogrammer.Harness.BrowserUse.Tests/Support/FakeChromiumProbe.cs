using Autonogrammer.Harness.BrowserUse.Chromium;

namespace Autonogrammer.Harness.BrowserUse.Tests.Support;

/// <summary>An in-memory file system for locator tests.</summary>
internal sealed class FakeChromiumProbe : IChromiumProbe
{
    private readonly Dictionary<string, int> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _links = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _directories = new(StringComparer.Ordinal);

    /// <summary>Records a file and its major version.</summary>
    internal void AddFile(string path, int major)
    {
        _files[path] = major;
    }

    /// <summary>Records a symlink target.</summary>
    internal void AddLink(string path, string target)
    {
        _links[path] = target;
    }

    /// <summary>Records child directory names.</summary>
    internal void AddDirectory(string path, string child)
    {
        if (!_directories.TryGetValue(path, out var children))
        {
            children = [];
            _directories[path] = children;
        }

        children.Add(child);
    }

    /// <summary>Removes every recorded file.</summary>
    internal void ClearFiles()
    {
        _files.Clear();
    }

    /// <summary>Removes every recorded directory.</summary>
    internal void ClearDirectories()
    {
        _directories.Clear();
    }

    /// <inheritdoc />
    public bool Exists(string path)
    {
        return _files.ContainsKey(path);
    }

    /// <inheritdoc />
    public string? ReadLink(string path)
    {
        return _links.TryGetValue(path, out var target) ? target : null;
    }

    /// <inheritdoc />
    public int? ReadMajorVersion(string path)
    {
        return _files.TryGetValue(path, out var major) ? major : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListDirectories(string path)
    {
        return _directories.TryGetValue(path, out var children) ? children : [];
    }
}
