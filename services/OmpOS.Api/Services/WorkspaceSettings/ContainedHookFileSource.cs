using OmpOS.Harness.Plugins.Hooks;
using OmpOS.Workspace;

namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>
/// The file access of the hook loader on the API. It reads through <see cref="LocalHookFileSource"/> but only inside the workspace folder and, when user
/// scope is on, the home directory, and only along paths that stay inside after every symbolic link is resolved. Anything else (the machine-wide managed
/// file, a link that leaves the folder) is reported as absent.
/// </summary>
internal sealed class ContainedHookFileSource : IHookFileSource
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly LocalHookFileSource _inner = new();
    private readonly List<AllowedRoot> _allowed = [];

    /// <summary>Initializes a new instance of the <see cref="ContainedHookFileSource"/> class.</summary>
    /// <param name="workspaceRoot">The existing workspace folder.</param>
    /// <param name="home">The existing home directory, or null to read nothing there.</param>
    /// <exception cref="DirectoryNotFoundException">A folder does not exist.</exception>
    public ContainedHookFileSource(string workspaceRoot, string? home)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _allowed.Add(new AllowedRoot(Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot)), new WorkspaceRoot(workspaceRoot)));
        if (!string.IsNullOrWhiteSpace(home))
        {
            _allowed.Add(new AllowedRoot(Path.TrimEndingDirectorySeparator(Path.GetFullPath(home)), new WorkspaceRoot(home)));
        }
    }

    /// <inheritdoc />
    public HookFileInfo Inspect(string path) =>
        Guard(path) is { } real ? _inner.Inspect(real) : new HookFileInfo(false, 0, false);

    /// <inheritdoc />
    public ValueTask<byte[]?> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken) =>
        Guard(path) is { } real ? _inner.ReadAsync(real, maxBytes, cancellationToken) : ValueTask.FromResult<byte[]?>(null);

    /// <inheritdoc />
    public ValueTask<string?> DigestAsync(string path, int maxBytes, CancellationToken cancellationToken) =>
        Guard(path) is { } real ? _inner.DigestAsync(real, maxBytes, cancellationToken) : ValueTask.FromResult<string?>(null);

    /// <summary>Resolves a path to its real path when it stays inside an allowed folder, otherwise null.</summary>
    private string? Guard(string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        foreach (var allowed in _allowed)
        {
            var prefix = allowed.Directory + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, PathComparison))
            {
                continue;
            }

            try
            {
                return allowed.Root.Resolve(full[prefix.Length..]);
            }
            catch (Exception exception) when (exception is WorkspacePathException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        return null;
    }

    private sealed record AllowedRoot(string Directory, WorkspaceRoot Root);
}
