using OmpOS.Workspace.Conventions;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Everything convention discovery needs to read a remote workspace: the two file trees, the project roots and the remote user's home. Pass
/// <see cref="Roots"/>, <see cref="UserHome"/> and <see cref="Sources"/> to <see cref="ConventionCatalog.DiscoverAsync"/> (or to the slash
/// discovery source), or call <see cref="DiscoverAsync"/>.
/// </summary>
/// <param name="Sources">The remote project tree (limited to the workspace roots) and the remote user tree (limited to the home directory).</param>
/// <param name="Roots">The remote workspace roots, absolute POSIX paths, in precedence order.</param>
/// <param name="UserHome">The home directory of the remote user, or <see langword="null"/> when it could not be resolved (user-scope conventions are then skipped).</param>
public sealed record RemoteConventionSetup(ConventionSources Sources, IReadOnlyList<string> Roots, string? UserHome)
{
    /// <summary>Discovers the commands, skills, rules and subagents of the remote workspace with bounds suited to a network round trip per folder.</summary>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    /// <returns>Everything found, and every problem.</returns>
    public Task<ConventionSet> DiscoverAsync(CancellationToken cancellationToken) =>
        ConventionCatalog.DiscoverAsync(Roots, UserHome, Sources, RemoteConventionSources.DiscoveryOptions, cancellationToken);
}
