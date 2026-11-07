namespace Autonogrammer.Workspace.Conventions;

/// <summary>
/// Discovers commands, skills, rules, subagents, hooks files and MCP files from every convention directory (<see cref="ConventionDirectories.Names"/>) at each workspace root and at
/// the user's home. Precedence: a project item beats a user item; within a scope the directories are tried in <see cref="ConventionDirectories.Names"/> order; an earlier
/// workspace root beats a later one; the first item with a name wins and the others are listed in <see cref="ConventionSet.Diagnostics"/>. A malformed, oversize or unreadable file is a diagnostic and never an
/// exception. The files are read through <see cref="IConventionFileSource"/>, so the same discovery serves a local disk, an SSH host and a cloud file tree. When <see cref="ConventionSources.Builtin"/> is set, the skills that
/// ship with the application are added after project and user items, in the <see cref="ConventionScope.Builtin"/> scope. When <see cref="ConventionSources.PluginRoots"/> is set, each bundle is read after that, in the <see cref="ConventionScope.Plugin"/> scope: skills keep their folder names, and commands and agents are named <c>&lt;plugin&gt;:&lt;command&gt;</c> and <c>&lt;plugin&gt;:&lt;agent&gt;</c>. A project, user or built-in item of the same name wins.
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

        if (sources.Builtin is { } builtin)
        {
            // The skills that ship with the application come last, so a project or user skill of the same name wins and the loser is reported as a duplicate.
            var context = new ConventionScanContext(builtin, collector, bounds, ConventionScope.Builtin, builtin.BasePath, "builtin:");
            await new ConventionItemScanner(context).ScanSkillsAsync(string.Empty, string.Empty, cancellationToken).ConfigureAwait(false);
        }

        if (sources.PluginRoots is { Count: > 0 } pluginRoots && sources.Plugin is { } pluginSource)
        {
            foreach (var root in pluginRoots)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                var pluginId = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var context = new ConventionScanContext(pluginSource, collector, bounds, ConventionScope.Plugin, root, "plugin:" + pluginId + ":");
                await new ConventionItemScanner(context).ScanSkillsAsync(string.Empty, string.Empty, cancellationToken).ConfigureAwait(false);
                await ScanPluginMarkdownAsync(pluginSource, collector, bounds, root, pluginId, "commands", ConventionItemKind.Command, cancellationToken).ConfigureAwait(false);
                await ScanPluginMarkdownAsync(pluginSource, collector, bounds, root, pluginId, "agents", ConventionItemKind.Subagent, cancellationToken).ConfigureAwait(false);
            }
        }

        return collector.Build();
    }

    private static async Task ScanPluginMarkdownAsync(
        IConventionFileSource source,
        ConventionCollector collector,
        ConventionDiscoveryOptions bounds,
        string root,
        string pluginId,
        string folder,
        ConventionItemKind kind,
        CancellationToken cancellationToken)
    {
        var directory = ConventionPath.Join(root, folder);
        var list = await source.ListAsync(directory, bounds.MaxEntriesPerDirectory, cancellationToken).ConfigureAwait(false);
        if (list.Problem is not null)
        {
            collector.AddDiagnostic(kind, "plugin:" + pluginId + ":" + folder, list.ProblemKind, list.Problem);
            return;
        }

        foreach (var entry in list.Entries)
        {
            if (entry.IsDirectory) continue;

            var stem = ConventionText.Stem(entry.Name, ".md");
            if (stem is null)
            {
                continue;
            }

            var relative = folder + "/" + entry.Name;
            var path = ConventionPath.Join(root, relative);
            var read = await source.ReadTextAsync(path, bounds.MaxFileBytes, cancellationToken).ConfigureAwait(false);
            if (!read.IsRead || read.Text is null)
            {
                collector.AddDiagnostic(kind, "plugin:" + pluginId + ":" + relative, read.ProblemKind, read.Problem ?? "the file could not be read");
                continue;
            }

            var markdown = ConventionFrontmatterParser.Parse(read.Text);
            var qualified = pluginId + ":" + stem;
            var origin = "plugin:" + pluginId + ":" + relative;
            if (kind == ConventionItemKind.Command)
            {
                collector.Add(new ConventionCommand(
                    qualified,
                    ConventionText.Describe(markdown),
                    markdown.ReadText("argument-hint"),
                    markdown.Body,
                    ConventionScope.Plugin,
                    string.Empty,
                    path,
                    origin));
            }
            else
            {
                collector.Add(new ConventionSubagent(
                    qualified,
                    ConventionText.Describe(markdown),
                    markdown.ReadText("model"),
                    markdown.ReadFlag("readonly") ?? false,
                    markdown.ReadFlag("is_background") ?? false,
                    markdown.Body,
                    ConventionScope.Plugin,
                    string.Empty,
                    path,
                    origin));
            }
        }
    }
}
