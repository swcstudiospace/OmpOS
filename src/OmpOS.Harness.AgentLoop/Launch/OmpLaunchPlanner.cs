using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Plugins;

namespace OmpOS.Harness.AgentLoop.Launch;

/// <summary>
/// Decides how omp is launched for one session. Planning is pure: it does path math and builds values, and creates, reads
/// and writes nothing. A plugin that is switched off, missing or unhealthy is left out and reported as a stage event, and
/// the plan is still produced, so a session always runs, at worst as plain omp.
/// </summary>
public static class OmpLaunchPlanner
{
    /// <summary>The name of the MCP server entry that reaches the substrate.</summary>
    public const string SubstrateMcpServerName = "substrate";

    /// <summary>The environment variable that tells the omp plugin where to serve the steering inbox of the session.</summary>
    public const string SteerDirectoryVariable = "OMPOS_STEER_DIR";

    /// <summary>The environment variable that tells the omp plugin where to write its connection report.</summary>
    public const string ConnectionFileVariable = "OMPOS_CONNECTION_FILE";

    /// <summary>
    /// Plans the launch of one session.
    /// </summary>
    /// <param name="settings">What the user asked of the agent loop.</param>
    /// <param name="plugins">The resolved plugins. They are loaded in composition order whatever order they are given in.</param>
    /// <param name="sessionId">The session identifier, a safe single path segment (see <see cref="SessionStateLayout"/>).</param>
    /// <param name="workspaceRoot">The absolute path of the workspace. It becomes the working directory of the child.</param>
    /// <param name="graphId">The Graph identifier of the session's plan, or null. It is only copied into the planning events.</param>
    /// <param name="timeProvider">The clock that stamps the planning events. The system clock when null.</param>
    /// <param name="additionalDirectories">
    /// The absolute paths of the other roots of a multi-root workspace, in order, or null for a single-root session. Each is passed
    /// to omp as its own <c>--add-dir</c> argument (the probe in <c>docs/runbooks/evidence/multi-repo-omp-probe-2026-10-27</c> proved
    /// that <c>omp acp</c> honours the flag), after every other argument. The workspace root itself is never one of them.
    /// </param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentException">
    /// The session identifier is unsafe, the workspace is not an absolute path, the isolated profile has no home directory, or an
    /// additional directory is blank, relative, contains NUL, repeats another, or is the workspace root.
    /// </exception>
    public static OmpLaunchPlan Plan(
        AgentLoopSettings settings,
        IReadOnlyList<ResolvedPlugin> plugins,
        string sessionId,
        string workspaceRoot,
        string? graphId,
        TimeProvider? timeProvider = null,
        IReadOnlyList<string>? additionalDirectories = null)
    {
        ValidateInputs(settings, plugins, sessionId, workspaceRoot);
        var extraDirectories = ValidateAdditionalDirectories(additionalDirectories, workspaceRoot);

        var sessionDirectory = SessionStateLayout.SessionDirectory(settings.StateRoot, sessionId);
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var events = new List<StageEvent>();
        var loaded = new List<string>(OmpPluginCatalog.BuiltIn.Count);
        var arguments = new List<string> { "--no-extensions" };
        var substrate = TryPlanSubstrate(settings);
        var substrateActive = false;

        foreach (var plugin in InCompositionOrder(plugins))
        {
            var descriptor = plugin.Descriptor;
            if (!settings.IsEnabled(descriptor.Id) || plugin.Health == PluginHealth.Disabled)
            {
                events.Add(PluginStageEvents.Create(descriptor, sessionId, graphId, StageStatus.Skipped, $"{descriptor.DisplayName} is switched off in the agent loop settings.", now));
                continue;
            }

            if (plugin.Health != PluginHealth.Ready)
            {
                events.Add(PluginStageEvents.Create(descriptor, sessionId, graphId, PluginStageEvents.StatusFor(plugin.Health), OmissionDetail(plugin), now));
                continue;
            }

            if (descriptor.Role == OmpPluginRole.SessionSubstrate)
            {
                if (substrate.Problem is { } problem)
                {
                    events.Add(PluginStageEvents.Create(descriptor, sessionId, graphId, problem.Status, problem.Detail, now));
                    continue;
                }

                substrateActive = true;
            }

            loaded.Add(descriptor.Id);
            AddEntryPaths(plugin, arguments);
        }

        var overlayPath = AddConfigOverlay(settings, sessionDirectory, arguments);
        AddDirectories(extraDirectories, arguments);

        return new OmpLaunchPlan
        {
            ExtraArguments = arguments,
            Environment = BuildEnvironment(settings, sessionDirectory, substrateActive ? substrate.BaseAddress : null),
            McpServers = substrateActive ? [BuildMcpServer(settings, substrate.BaseAddress!)] : [],
            WorkingDirectory = workspaceRoot,
            ConfigOverlayPath = overlayPath,
            AdditionalDirectories = extraDirectories,
            LoadedPluginIds = loaded,
            PlanningEvents = events,
        };
    }

    private static void AddEntryPaths(ResolvedPlugin plugin, List<string> arguments)
    {
        foreach (var entry in plugin.EntryPaths)
        {
            arguments.Add("-e");
            arguments.Add(entry);
        }
    }

    private static void AddDirectories(IReadOnlyList<string> directories, List<string> arguments)
    {
        foreach (var directory in directories)
        {
            arguments.Add("--add-dir");
            arguments.Add(directory);
        }
    }

    private static string? AddConfigOverlay(AgentLoopSettings settings, string sessionDirectory, List<string> arguments)
    {
        if (!settings.DisableProjectMcpConfig)
        {
            return null;
        }

        var overlayPath = SessionStateLayout.ConfigOverlayFile(sessionDirectory);
        arguments.Add("--config");
        arguments.Add(overlayPath);
        return overlayPath;
    }

    private static void ValidateInputs(AgentLoopSettings settings, IReadOnlyList<ResolvedPlugin> plugins, string sessionId, string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (!Path.IsPathRooted(workspaceRoot))
        {
            throw new ArgumentException("The workspace root must be an absolute path.", nameof(workspaceRoot));
        }

        if (settings.Profile == AgentLoopProfile.Isolated && string.IsNullOrWhiteSpace(settings.IsolatedHomeDirectory))
        {
            throw new ArgumentException("The isolated profile needs an isolated home directory.", nameof(settings));
        }
    }

    private static List<string> ValidateAdditionalDirectories(IReadOnlyList<string>? additionalDirectories, string workspaceRoot)
    {
        var validated = new List<string>(additionalDirectories?.Count ?? 0);
        if (additionalDirectories is null)
        {
            return validated;
        }

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var seen = new HashSet<string>(comparer) { TrimSeparator(workspaceRoot) };
        foreach (var directory in additionalDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory) || directory.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException("An additional directory must be a non-empty path without NUL characters.", nameof(additionalDirectories));
            }

            if (!Path.IsPathRooted(directory))
            {
                throw new ArgumentException("An additional directory must be an absolute path.", nameof(additionalDirectories));
            }

            if (!seen.Add(TrimSeparator(directory)))
            {
                throw new ArgumentException("An additional directory repeats another root or the workspace root.", nameof(additionalDirectories));
            }

            validated.Add(directory);
        }

        return validated;
    }

    private static string TrimSeparator(string path)
    {
        var root = Path.GetPathRoot(path);
        return path.Length > (root?.Length ?? 0) ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
    }

    private static IEnumerable<ResolvedPlugin> InCompositionOrder(IReadOnlyList<ResolvedPlugin> plugins)
    {
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < OmpPluginCatalog.BuiltIn.Count; index++)
        {
            order[OmpPluginCatalog.BuiltIn[index].Id] = index;
        }

        return plugins
            .Select((plugin, index) => (plugin, index))
            .OrderBy(pair => order.GetValueOrDefault(pair.plugin.Descriptor.Id, int.MaxValue))
            .ThenBy(pair => pair.index)
            .Select(pair => pair.plugin);
    }

    private static string OmissionDetail(ResolvedPlugin plugin)
    {
        var consequence = plugin.Descriptor.Required
            ? "It is needed for the full agent loop; the session runs without it."
            : "It is optional; the session runs without it.";
        return plugin.Message + " " + consequence;
    }

    private static SubstratePlan TryPlanSubstrate(AgentLoopSettings settings)
    {
        if (!settings.SubstrateEnabled)
        {
            return default;
        }

        var endpoint = settings.SubstrateEndpoint;
        if (endpoint is null)
        {
            return Unusable(StageStatus.Unavailable, "No substrate endpoint is configured, so the substrate is left out of this session.");
        }

        if (!endpoint.IsAbsoluteUri
            || !IsHttp(endpoint)
            || !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            return Unusable(StageStatus.Unavailable, "The substrate endpoint must be an absolute http or https address without user information, so the substrate is left out of this session.");
        }

        if (!settings.SubstrateReachable)
        {
            var reason = string.IsNullOrWhiteSpace(settings.SubstrateUnreachableReason)
                ? "The substrate did not answer its health check."
                : settings.SubstrateUnreachableReason;
            return Unusable(StageStatus.Degraded, reason + " The substrate is left out of this session: no steering record and no shared memory.");
        }

        return new SubstratePlan(endpoint.GetLeftPart(UriPartial.Path).TrimEnd('/'), null);
    }

    private static bool IsHttp(Uri endpoint) =>
        string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
        || string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);

    private static SubstratePlan Unusable(StageStatus status, string detail) => new(null, new SubstrateProblem(status, detail));

    private static Dictionary<string, string> BuildEnvironment(AgentLoopSettings settings, string sessionDirectory, string? substrateBase)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        if (settings.IsolatedHomeDirectory is { } home && !string.IsNullOrWhiteSpace(home))
        {
            environment["HOME"] = Path.Combine(home, "home");
            environment["PI_CODING_AGENT_DIR"] = Path.Combine(home, "agent");
        }

        environment["ULTRATHINK_STATE_DIR"] = SessionStateLayout.UltrathinkDirectory(sessionDirectory);
        environment["SWARM_DIR"] = SessionStateLayout.SwarmDirectory(sessionDirectory);
        environment["SWARM_GSD_STATE_FILE"] = SessionStateLayout.SwarmGsdStateFile(sessionDirectory);
        environment["SWARM_CLAUDE_STATE_DIR"] = SessionStateLayout.ClaudeStateDirectory(sessionDirectory);
        environment["SUBSTRATE_OMP_PENDING_DIR"] = SessionStateLayout.SubstratePendingDirectory(sessionDirectory);
        environment[SteerDirectoryVariable] = SessionStateLayout.SteerDirectory(sessionDirectory);
        environment[ConnectionFileVariable] = SessionStateLayout.ConnectionFile(sessionDirectory);

        if (!settings.UltrathinkEnabled)
        {
            environment["ULTRATHINK_UPLIFT"] = "0";
        }

        if (substrateBase is not null)
        {
            environment["SUBSTRATE_URL"] = substrateBase;
            if (!string.IsNullOrWhiteSpace(settings.SubstrateTokensDirectory))
            {
                environment["SUBSTRATE_TOKENS_DIR"] = settings.SubstrateTokensDirectory;
            }
        }

        if (settings.Profile == AgentLoopProfile.Isolated)
        {
            AddIsolatedProfile(environment, settings.IsolatedHomeDirectory!, substrateBase is not null);
        }

        return environment;
    }

    private static void AddIsolatedProfile(Dictionary<string, string> environment, string home, bool substrateActive)
    {
        var userHome = Path.Combine(home, "home");
        environment["XDG_CONFIG_HOME"] = Path.Combine(userHome, ".config");
        environment["XDG_DATA_HOME"] = Path.Combine(userHome, ".local", "share");
        environment["XDG_CACHE_HOME"] = Path.Combine(userHome, ".cache");
        environment["XDG_STATE_HOME"] = Path.Combine(userHome, ".local", "state");
        environment["ULTRATHINK_TRACK"] = "0";
        environment["ULTRATHINK_SHIP"] = "0";
        environment["ULTRATHINK_DECISIONS"] = "0";
        environment["ULTRATHINK_HINDSIGHT"] = "0";
        environment["ULTRATHINK_RAGFLOW"] = "0";
        environment["ULTRATHINK_TEACH"] = "0";
        environment["ULTRATHINK_GATEWAY"] = "0";
        environment["ULTRATHINK_SWARM_ROOT"] = string.Empty;
        if (!substrateActive)
        {
            environment["SUBSTRATE_DISABLED"] = "1";
        }
    }

    private static AgentMcpServerSpec BuildMcpServer(AgentLoopSettings settings, string substrateBase)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(settings.SubstrateToken))
        {
            headers["Authorization"] = "Bearer " + settings.SubstrateToken;
        }

        return new AgentMcpServerSpec
        {
            Name = SubstrateMcpServerName,
            Url = new Uri(substrateBase + "/mcp", UriKind.Absolute),
            Headers = headers,
        };
    }

    private readonly record struct SubstratePlan(string? BaseAddress, SubstrateProblem? Problem);

    private sealed record SubstrateProblem(StageStatus Status, string Detail);
}
