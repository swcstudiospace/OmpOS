namespace OmpOS.Workspace.Sets;

/// <summary>
/// Answers <see cref="IDirectoryProbe"/> from the local file system. Real paths use the same symbolic-link walk as
/// <see cref="WorkspaceRoot"/>.
/// </summary>
public sealed class LocalDirectoryProbe : IDirectoryProbe
{
    /// <inheritdoc />
    public bool Exists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Directory.Exists(path);
    }

    /// <inheritdoc />
    public string? RealPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Directory.Exists(path))
        {
            return null;
        }

        try
        {
            return new WorkspaceRoot(path).FullPath;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool IsGitRoot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var marker = Path.Combine(path, ".git");
        return Directory.Exists(marker) || File.Exists(marker);
    }
}
