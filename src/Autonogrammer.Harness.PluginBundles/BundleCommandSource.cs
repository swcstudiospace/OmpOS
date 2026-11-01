namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Maps bundle commands, agents and hooks. There is no method that runs a hook.</summary>
public sealed class BundleCommandSource
{
    /// <summary>The state text for a listed hook file.</summary>
    public const string HooksNotLoaded = "BundleComponentNotLoaded(hooks)";

    /// <summary>The collision text when a built-in wins.</summary>
    public const string NameCollision = "BundleInvalid(BundleNameCollision)";

    private readonly IReadOnlySet<string> _reservedCommands;

    /// <summary>Initializes a new instance of the <see cref="BundleCommandSource"/> class.</summary>
    /// <param name="reservedCommands">Built-in command ids. A bundle command with the same stem is suppressed.</param>
    public BundleCommandSource(IReadOnlySet<string>? reservedCommands = null)
    {
        _reservedCommands = reservedCommands ?? new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Lists <c>commands/*.md</c> as <c>/plugin:command</c>.</summary>
    /// <param name="bundleDirectory">The installed bundle.</param>
    /// <param name="pluginId">The bundle id.</param>
    /// <returns>The commands, in ordinal file order.</returns>
    public IReadOnlyList<BundleCommandListing> ListCommands(string bundleDirectory, string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        var directory = Path.Combine(bundleDirectory, "commands");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var listings = new List<BundleCommandListing>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.md").Order(StringComparer.Ordinal))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            var suppressed = _reservedCommands.Contains(stem);
            listings.Add(new BundleCommandListing(
                "/" + pluginId + ":" + stem,
                "commands/" + Path.GetFileName(file),
                suppressed,
                suppressed ? NameCollision : null));
        }

        return listings;
    }

    /// <summary>Lists <c>agents/*.md</c> with plugin-qualified names.</summary>
    /// <param name="bundleDirectory">The installed bundle.</param>
    /// <param name="pluginId">The bundle id.</param>
    /// <returns>The agents, in ordinal file order.</returns>
    public static IReadOnlyList<BundleAgentListing> ListAgents(string bundleDirectory, string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        var directory = Path.Combine(bundleDirectory, "agents");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(directory, "*.md").Order(StringComparer.Ordinal).Select(file =>
            new BundleAgentListing(pluginId + ":" + Path.GetFileNameWithoutExtension(file), "agents/" + Path.GetFileName(file)))];
    }

    /// <summary>Lists <c>hooks/hooks.json</c> when it exists. The content is returned as text and is never executed.</summary>
    /// <param name="bundleDirectory">The installed bundle.</param>
    /// <returns>Zero or one listing.</returns>
    public static IReadOnlyList<BundleHookListing> ListHooks(string bundleDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        var path = Path.Combine(bundleDirectory, "hooks", "hooks.json");
        if (!File.Exists(path))
        {
            return [];
        }

        return [new BundleHookListing("hooks/hooks.json", File.ReadAllText(path), HooksNotLoaded)];
    }
}
