using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;

namespace Autonogrammer.Cowork.Chat;

/// <summary>
/// The Cowork launch. It is not the Code planner: no extensions, an asking approval mode, one <c>--add-dir</c> per trusted
/// folder, the absolute prompt path, and a config overlay that disables browser and computer.
/// </summary>
public sealed class CoworkSessionProfile
{
    /// <summary>The overlay the composer writes with <see cref="ConfigOverlayWriter.WriteYaml"/>.</summary>
    public const string OverlayYaml = "browser:\n  enabled: false\ncomputer:\n  enabled: false\n";

    private const string ProviderPrefix = "AUTONOGRAMMER_PROVIDER_";
    private const string ProviderSuffix = "_KEY";

    private readonly string _sessionDirectory;
    private readonly string _workingDirectory;
    private readonly IReadOnlyList<string> _trustedFolders;
    private readonly Tools.CoworkPermissionMode _mode;
    private readonly IReadOnlyDictionary<string, string> _providerKeys;
    private readonly Uri? _gatewayUrl;
    private readonly string? _gatewayBearer;

    /// <summary>Initializes a new instance of the <see cref="CoworkSessionProfile"/> class.</summary>
    /// <param name="sessionDirectory">The session directory the prompt file is written into.</param>
    /// <param name="workingDirectory">The working directory of the child.</param>
    /// <param name="trustedFolders">The folders passed as <c>--add-dir</c>.</param>
    /// <param name="mode">The permission mode. The default is Manual.</param>
    /// <param name="providerKeys">The composer-supplied environment. Only <c>AUTONOGRAMMER_PROVIDER_*_KEY</c> names are kept.</param>
    /// <param name="gatewayUrl">The cowork gateway URL, or null when the profile carries no MCP server.</param>
    /// <param name="gatewayBearer">The bearer for the cowork gateway. Required when <paramref name="gatewayUrl"/> is set.</param>
    /// <param name="filesystemIsolated">Whether the folder boundary is FilesystemIsolated. The resolver reads this; the launch flags do not.</param>
    /// <param name="skipAcknowledged">Whether the user acknowledged Skip for this session.</param>
    public CoworkSessionProfile(
        string sessionDirectory,
        string workingDirectory,
        IReadOnlyList<string> trustedFolders,
        Tools.CoworkPermissionMode mode = Tools.CoworkPermissionMode.Manual,
        IReadOnlyDictionary<string, string>? providerKeys = null,
        Uri? gatewayUrl = null,
        string? gatewayBearer = null,
        bool filesystemIsolated = false,
        bool skipAcknowledged = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(trustedFolders);
        _sessionDirectory = sessionDirectory;
        _workingDirectory = workingDirectory;
        _trustedFolders = trustedFolders;
        _mode = mode;
        _providerKeys = providerKeys ?? new Dictionary<string, string>(StringComparer.Ordinal);
        _gatewayUrl = gatewayUrl;
        _gatewayBearer = gatewayBearer;
        FilesystemIsolated = filesystemIsolated;
        SkipAcknowledged = skipAcknowledged;
    }

    /// <summary>Gets whether the folder boundary is FilesystemIsolated.</summary>
    public bool FilesystemIsolated { get; }

    /// <summary>Gets whether the user acknowledged Skip for this session.</summary>
    public bool SkipAcknowledged { get; }

    /// <summary>Whether a name is an <c>AUTONOGRAMMER_PROVIDER_*_KEY</c> variable.</summary>
    /// <param name="name">The environment variable name.</param>
    /// <returns>True when the name has the provider-key shape.</returns>
    public static bool IsProviderKeyName(string name)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith(ProviderPrefix, StringComparison.Ordinal) || !name.EndsWith(ProviderSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        if (name.Length <= ProviderPrefix.Length + ProviderSuffix.Length)
        {
            return false;
        }

        foreach (var character in name.AsSpan(ProviderPrefix.Length, name.Length - ProviderPrefix.Length - ProviderSuffix.Length))
        {
            if (character is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Keeps provider keys and refuses every other name. The refusal names the variable, never its value.
    /// </summary>
    /// <param name="environment">The composer-supplied dictionary.</param>
    /// <returns>The provider keys, and nothing else.</returns>
    /// <exception cref="CoworkEnvironmentRefusedException">A name is not a provider key.</exception>
    public static IReadOnlyDictionary<string, string> FilterProviderKeys(IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in environment)
        {
            if (!IsProviderKeyName(pair.Key))
            {
                throw new CoworkEnvironmentRefusedException(pair.Key);
            }

            kept[pair.Key] = pair.Value;
        }

        return kept;
    }

    /// <summary>
    /// Writes the prompt file and returns the launch. The start is refused when the prompt cannot be written.
    /// Contributors (if any) are passed to OmpLaunchPlanner so the seam is applied generically before Cowork specifics are added.
    /// </summary>
    /// <param name="standingContext">The standing-context paragraph, or null.</param>
    /// <param name="browserGuidance">Browser guidance, or null.</param>
    /// <param name="computerGuidance">Computer guidance, or null.</param>
    /// <param name="contributors">Launch contributors to apply generically via the planner (surface will be "cowork").</param>
    /// <returns>The launch plan.</returns>
    /// <exception cref="CoworkPromptUnavailableException">The prompt file cannot be written.</exception>
    /// <exception cref="CoworkEnvironmentRefusedException">The environment holds a name that is not a provider key.</exception>
    /// <exception cref="ArgumentException">A gateway URL is set without a bearer, or a bearer without a URL.</exception>
    public OmpLaunchPlan Create(string? standingContext = null, string? browserGuidance = null, string? computerGuidance = null, IReadOnlyList<ILaunchContributor>? contributors = null)
    {
        var environment = FilterProviderKeys(_providerKeys);
        if ((_gatewayUrl is null) != string.IsNullOrEmpty(_gatewayBearer))
        {
            throw new InvalidOperationException("The cowork gateway needs both a URL and a bearer, or neither.");
        }

        var promptPath = CoworkSystemPrompt.Write(_sessionDirectory, standingContext, browserGuidance, computerGuidance);
        var overlayPath = SessionStateLayout.ConfigOverlayFile(Path.GetFullPath(_sessionDirectory));

        // The planner applies contributed arguments and environment; overlay and prompt contribution delivery remains incomplete.
        var basePlan = OmpLaunchPlanner.Plan(
            new AgentLoopSettings { PluginsRoot = string.Empty, StateRoot = _sessionDirectory },
            Array.Empty<ResolvedPlugin>(),
            "ignored-for-profile",
            Path.GetFullPath(_workingDirectory),
            null,
            contributors: contributors,
            surface: "cowork",
            includeSessionEnvironment: false);

        var arguments = new List<string>(basePlan.ExtraArguments);
        arguments.Add("--approval-mode");
        arguments.Add(CoworkPermissionMode.ApprovalModeArgument(_mode));
        foreach (var folder in _trustedFolders)
        {
            arguments.Add("--add-dir");
            arguments.Add(Path.GetFullPath(folder));
        }

        arguments.Add("--append-system-prompt");
        arguments.Add(promptPath);
        arguments.Add("--config");
        arguments.Add(overlayPath);

        // Contributors cannot expand the provider-only Cowork environment; configured provider keys take precedence.
        var finalEnv = new Dictionary<string, string>(FilterProviderKeys(basePlan.Environment), StringComparer.Ordinal);
        foreach (var kv in environment)
        {
            finalEnv[kv.Key] = kv.Value;
        }

        return new OmpLaunchPlan
        {
            ExtraArguments = arguments,
            Environment = finalEnv,
            McpServers = Servers(),
            WorkingDirectory = Path.GetFullPath(_workingDirectory),
            ConfigOverlayPath = overlayPath,
            AdditionalDirectories = _trustedFolders.Select(Path.GetFullPath).ToArray(),
            PlanningEvents = basePlan.PlanningEvents,
            LoadedPluginIds = basePlan.LoadedPluginIds,
            HooksBridgeLoaded = basePlan.HooksBridgeLoaded,
        };
    }

    private IReadOnlyList<AgentMcpServerSpec> Servers()
    {
        if (_gatewayUrl is null || _gatewayBearer is null)
        {
            return [];
        }

        return
        [
            new AgentMcpServerSpec
            {
                Name = "cowork",
                Url = _gatewayUrl,
                Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer " + _gatewayBearer },
            },
        ];
    }
}
