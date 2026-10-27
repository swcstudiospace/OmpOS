using OmpOS.Presentation.Commands;
using OmpOS.Workspace.Conventions;

namespace OmpOS.Native.Views.Conventions;

/// <summary>
/// Reads the commands, skills and subagents of a session's workspace through <see cref="ConventionCatalog"/>. It reads the local disk by default; a remote or cloud head passes the sources of its own file tree.
/// Each <see cref="LoadAsync"/> reads again, so calling it is the manual refresh.
/// </summary>
public sealed class ConventionSlashDiscoverySource : ISlashDiscoverySource
{
    private readonly IReadOnlyList<string> _roots;
    private readonly string? _userHome;
    private readonly ConventionSources _sources;

    /// <summary>Initializes a new instance of the <see cref="ConventionSlashDiscoverySource"/> class.</summary>
    /// <param name="roots">The workspace roots of the session.</param>
    /// <param name="userHome">The user's home directory, or <see langword="null"/> to skip user conventions.</param>
    /// <param name="sources">The file trees to read, or <see langword="null"/> for the local disk.</param>
    /// <exception cref="ArgumentNullException"><paramref name="roots"/> is null.</exception>
    public ConventionSlashDiscoverySource(IReadOnlyList<string> roots, string? userHome, ConventionSources? sources = null)
    {
        ArgumentNullException.ThrowIfNull(roots);
        _roots = roots;
        _userHome = userHome;
        _sources = sources ?? ConventionSources.Local(roots, userHome);
    }

    /// <inheritdoc/>
    public async Task<SlashDiscovery> LoadAsync(CancellationToken cancellationToken)
    {
        var set = await ConventionCatalog.DiscoverAsync(_roots, _userHome, _sources, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ConventionMapping.ToDiscovery(set);
    }
}
