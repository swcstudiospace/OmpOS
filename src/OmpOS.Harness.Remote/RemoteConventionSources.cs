using OmpOS.Workspace.Conventions;

namespace OmpOS.Harness.Remote;

/// <summary>Creates the convention file trees of an ssh host.</summary>
public static class RemoteConventionSources
{
    /// <summary>
    /// Gets the discovery bounds for a remote tree: every folder listing is an ssh round trip, so the nested walk is shallower and smaller than
    /// the local default (<see cref="ConventionDiscoveryOptions.Default"/>).
    /// </summary>
    public static ConventionDiscoveryOptions DiscoveryOptions { get; } = new()
    {
        MaxNestedDepth = 3,
        MaxNestedDirectories = 300,
        MaxEntriesPerDirectory = 1000,
    };

    /// <summary>Creates the sources of a remote session: both trees are on the remote machine, the user tree is the remote home directory.</summary>
    /// <param name="exec">Runs the scripts on the remote machine.</param>
    /// <param name="workspaceRoots">The remote workspace roots, or <see langword="null"/> for the remote workspace root of the profile.</param>
    /// <param name="cancellationToken">Cancels the lookup of the home directory.</param>
    /// <returns>The sources, the roots and the home directory (<c>printf %s "$HOME"</c> on the remote machine; null when it cannot be read).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exec"/> is null.</exception>
    public static async Task<RemoteConventionSetup> CreateAsync(RemoteExec exec, IReadOnlyList<string>? workspaceRoots, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exec);
        IReadOnlyList<string> roots = workspaceRoots is { Count: > 0 } ? workspaceRoots : new[] { exec.Profile.RemoteWorkspaceRoot };
        var home = await exec.ResolveHomeDirectoryAsync(cancellationToken).ConfigureAwait(false);
        var sources = new ConventionSources(
            new RemoteConventionFileSource(exec, roots),
            new RemoteConventionFileSource(exec, home is null ? Array.Empty<string>() : new[] { home }));
        return new RemoteConventionSetup(sources, roots, home);
    }
}
