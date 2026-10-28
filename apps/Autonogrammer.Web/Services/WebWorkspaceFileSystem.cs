using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The <see cref="IWorkspaceFileSystem"/> the Changes bar uses in the browser. A page cannot touch the workspace folder, which lives on the API
/// host, so this is an in-memory mirror of the files the agent changed: it holds the text the agent left, and a write or delete changes only the
/// mirror. The review model's Keep and Undo decisions reach the workspace through <see cref="WebChangeReview"/>, which sends them to the API.
/// <see cref="LocalWorkspaceFileSystem"/> is never used in the browser.
/// </summary>
internal sealed class WebWorkspaceFileSystem : IWorkspaceFileSystem
{
    /// <summary>The root the mirror reports. It is a label: no file below it exists in the browser.</summary>
    internal const string VirtualRoot = "/workspace";

    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string RootPath => VirtualRoot;

    /// <summary>Replaces the mirror with the text the agent left in each file.</summary>
    /// <param name="files">The workspace-relative path and the text of each changed file.</param>
    internal void Reset(IEnumerable<KeyValuePair<string, string>> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files.Clear();
        foreach (var (path, text) in files)
        {
            _files[path] = text;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<WorkspaceEntry> ListDirectory(string relativeDirectory) => [];

    /// <inheritdoc />
    public string ReadFile(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return _files.TryGetValue(relativePath, out var text)
            ? text
            : throw new FileNotFoundException("The file is not one of the agent's changes.", relativePath);
    }

    /// <inheritdoc />
    public void WriteFile(string relativePath, string content, string expectedCurrent)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(expectedCurrent);
        if (!_files.TryGetValue(relativePath, out var current) || !string.Equals(current, expectedCurrent, StringComparison.Ordinal))
        {
            throw new StaleWorkspaceFileException("The file changed since it was read.");
        }

        _files[relativePath] = content;
    }

    /// <inheritdoc />
    public void CreateFile(string relativePath, string content)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        _files[relativePath] = content;
    }

    /// <inheritdoc />
    public void DeleteFile(string relativePath, string expectedCurrent)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(expectedCurrent);
        if (!_files.TryGetValue(relativePath, out var current) || !string.Equals(current, expectedCurrent, StringComparison.Ordinal))
        {
            throw new StaleWorkspaceFileException("The file changed since it was read.");
        }

        _ = _files.Remove(relativePath);
    }
}
