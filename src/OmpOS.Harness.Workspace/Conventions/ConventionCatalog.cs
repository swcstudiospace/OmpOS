namespace OmpOS.Workspace.Conventions;

/// <summary>
/// Discovers commands, skills, rules, subagents, hooks files and MCP files from every convention directory (<see cref="ConventionDirectories.Names"/>) at each workspace root and at
/// the user's home. Precedence: a project item beats a user item; within a scope the directories are tried in <see cref="ConventionDirectories.Names"/> order; an earlier
/// workspace root beats a later one; the first item with a name wins and the others are listed in <see cref="ConventionSet.Diagnostics"/>. A malformed, oversize or unreadable file is a diagnostic and never an
/// exception. The files are read through <see cref="IConventionFileSource"/>, so the same discovery serves a local disk, an SSH host and a cloud file tree.
/// </summary>
public static class ConventionCatalog
{
    /// <summary>Discovers the conventions of a session.</summary>
    /// <param name="roots">The workspace roots, in precedence order, in the project source's path form.</param>
    /// <param name="userHome">The user's home directory in the user source's path form, or <see langword="null"/> to skip user conventions.</param>
    /// <param name="sources">The file trees to read.</param>
    /// <param name="options">The bounds of the run, or <see langword="null"/> for <see cref="ConventionDiscoveryOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    /// <returns>Everything found, and every problem.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="roots"/> or <paramref name="sources"/> is null.</exception>
    public static async Task<ConventionSet> DiscoverAsync(
        IReadOnlyList<string> roots,
        string? userHome,
        ConventionSources sources,
        ConventionDiscoveryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(sources);

        var bounds = options ?? ConventionDiscoveryOptions.Default;
        var collector = new ConventionCollector();
        var scanned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (!string.IsNullOrWhiteSpace(root) && scanned.Add(root))
            {
                var context = new ConventionScanContext(sources.Project, collector, bounds, ConventionScope.Project, root, string.Empty);
                await new ConventionScanner(context).ScanAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (!string.IsNullOrWhiteSpace(userHome))
        {
            var context = new ConventionScanContext(sources.User, collector, bounds, ConventionScope.User, userHome, "~/");
            await new ConventionScanner(context).ScanAsync(cancellationToken).ConfigureAwait(false);
        }

        return collector.Build();
    }
}
