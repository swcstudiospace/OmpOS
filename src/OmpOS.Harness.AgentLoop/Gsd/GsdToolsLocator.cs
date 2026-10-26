namespace OmpOS.Harness.AgentLoop.Gsd;

/// <summary>
/// Finds the workflow engine's <c>gsd-tools.cjs</c> script. An explicit location is used as given and never falls back, so a
/// wrong setting is not hidden by another install. Without one, the first existing candidate wins: <c>GSD_CORE_HOME</c>, then
/// <c>KIMI_CONFIG_DIR/gsd-core</c>, then <c>~/.config/agents/gsd-core</c>, then <c>~/.agents/gsd-core</c>.
/// </summary>
public sealed class GsdToolsLocator
{
    /// <summary>The environment variable that names the install directory.</summary>
    public const string HomeVariableName = "GSD_CORE_HOME";

    /// <summary>The environment variable of the Kimi installer's configuration directory.</summary>
    public const string KimiConfigVariableName = "KIMI_CONFIG_DIR";

    private readonly string? _explicitPath;
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly string? _homeDirectory;

    /// <summary>Initializes a new instance of the <see cref="GsdToolsLocator"/> class that reads the real environment.</summary>
    /// <param name="explicitPath">The explicit location, or null to search.</param>
    public GsdToolsLocator(string? explicitPath = null)
        : this(explicitPath, static name => Environment.GetEnvironmentVariable(name), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GsdToolsLocator"/> class with an injected environment.</summary>
    /// <param name="explicitPath">The explicit location, or null to search.</param>
    /// <param name="getEnvironmentVariable">Reads an environment variable, returning null when it is unset.</param>
    /// <param name="homeDirectory">The user's home directory, or null when unknown.</param>
    internal GsdToolsLocator(string? explicitPath, Func<string, string?> getEnvironmentVariable, string? homeDirectory)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        _explicitPath = explicitPath;
        _getEnvironmentVariable = getEnvironmentVariable;
        _homeDirectory = homeDirectory;
    }

    /// <summary>Resolves the script.</summary>
    /// <returns>The absolute path of <c>gsd-tools.cjs</c>, or null when it is not installed.</returns>
    public string? Locate()
    {
        if (!string.IsNullOrWhiteSpace(_explicitPath))
        {
            return ResolveExplicit(Path.GetFullPath(_explicitPath));
        }

        foreach (var home in CandidateHomes())
        {
            var script = ScriptIn(home);
            if (File.Exists(script))
            {
                return script;
            }
        }

        return null;
    }

    private static string ScriptIn(string home) => Path.Combine(home, "bin", "gsd-tools.cjs");

    private static string? ResolveExplicit(string path)
    {
        if (File.Exists(path))
        {
            return path;
        }

        var script = ScriptIn(path);
        return File.Exists(script) ? script : null;
    }

    private IEnumerable<string> CandidateHomes()
    {
        var home = _getEnvironmentVariable(HomeVariableName);
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.GetFullPath(home);
        }

        var kimi = _getEnvironmentVariable(KimiConfigVariableName);
        if (!string.IsNullOrWhiteSpace(kimi))
        {
            yield return Path.Combine(Path.GetFullPath(kimi), "gsd-core");
        }

        if (!string.IsNullOrWhiteSpace(_homeDirectory))
        {
            yield return Path.Combine(_homeDirectory, ".config", "agents", "gsd-core");
            yield return Path.Combine(_homeDirectory, ".agents", "gsd-core");
        }
    }
}
