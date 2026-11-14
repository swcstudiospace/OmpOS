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
    private const string DefaultSessionId = "cowork-session";

    private readonly string _sessionDirectory;
    private readonly string _workingDirectory;
    private readonly IReadOnlyList<string> _trustedFolders;
    private readonly Tools.CoworkPermissionMode _mode;
    private readonly IReadOnlyDictionary<string, string> _providerKeys;
    private readonly Uri? _gatewayUrl;
    private readonly string? _gatewayBearer;
    private readonly string _sessionId;

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
    /// <param name="sessionId">The session identifier the launch plan names. It is the real session when the caller has one, and "cowork-session" when null.</param>
    public CoworkSessionProfile(
        string sessionDirectory,
        string workingDirectory,
        IReadOnlyList<string> trustedFolders,
        Tools.CoworkPermissionMode mode = Tools.CoworkPermissionMode.Manual,
        IReadOnlyDictionary<string, string>? providerKeys = null,
        Uri? gatewayUrl = null,
        string? gatewayBearer = null,
        bool filesystemIsolated = false,
        bool skipAcknowledged = false,
        string? sessionId = null)
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
        _sessionId = sessionId ?? DefaultSessionId;
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
    /// Plans the launch once, then writes the prompt file and the one effective overlay, and returns the launch. The contributors go through
    /// <see cref="OmpLaunchPlanner"/>, so their paragraphs, first-prompt blocks, overlay entries, environment, arguments and wrapper reach the launch unchanged.
    /// The prompt carries the contributed paragraphs. The overlay merges the Cowork browser and computer settings with the contributed entries, at the single
    /// path that <c>--config</c> names. The start is refused when the prompt cannot be written.
    /// </summary>
    /// <param name="contributors">Launch contributors, applied by the planner as the surface "cowork".</param>
    /// <param name="browserGuidance">Browser guidance, or null.</param>
    /// <param name="computerGuidance">Computer guidance, or null.</param>
    /// <returns>The launch plan.</returns>
    /// <exception cref="CoworkPromptUnavailableException">The prompt file cannot be written.</exception>
    /// <exception cref="CoworkEnvironmentRefusedException">The environment holds a name that is not a provider key.</exception>
    /// <exception cref="ArgumentException">A gateway URL is set without a bearer, or a bearer without a URL.</exception>
    /// <exception cref="InvalidOperationException">Two contributions conflict, or two overlay entries set one top-level key.</exception>
    public OmpLaunchPlan Create(IReadOnlyList<ILaunchContributor>? contributors = null, string? browserGuidance = null, string? computerGuidance = null)
    {
        var environment = FilterProviderKeys(_providerKeys);
        if ((_gatewayUrl is null) != string.IsNullOrEmpty(_gatewayBearer))
        {
            throw new InvalidOperationException("The cowork gateway needs both a URL and a bearer, or neither.");
        }

        var workingDirectory = Path.GetFullPath(_workingDirectory);
        var sessionDirectory = Path.GetFullPath(_sessionDirectory);
        var basePlan = OmpLaunchPlanner.Plan(
            new AgentLoopSettings { PluginsRoot = string.Empty, StateRoot = _sessionDirectory },
            Array.Empty<ResolvedPlugin>(),
            _sessionId,
            workingDirectory,
            null,
            contributors: contributors,
            surface: "cowork",
            includeSessionEnvironment: false);

        // Contributors cannot expand the provider-only Cowork environment. The names are checked before anything is written.
        var contributedEnvironment = FilterProviderKeys(basePlan.Environment);
        var promptPath = CoworkSystemPrompt.Write(sessionDirectory, JoinParagraphs(basePlan.SystemPromptParagraphs), browserGuidance, computerGuidance);
        var overlayPath = SessionStateLayout.ConfigOverlayFile(sessionDirectory);
        ConfigOverlayWriter.WriteYaml(sessionDirectory, ConfigOverlayWriter.Merge([OverlayYaml, .. basePlan.OverlayEntries]));

        return new OmpLaunchPlan
        {
            ExtraArguments = LaunchArguments(basePlan, promptPath, overlayPath),
            Environment = LaunchEnvironment(contributedEnvironment, environment),
            McpServers = Servers(),
            WorkingDirectory = workingDirectory,
            ConfigOverlayPath = overlayPath,
            AdditionalDirectories = _trustedFolders.Select(Path.GetFullPath).ToArray(),
            PlanningEvents = basePlan.PlanningEvents,
            LoadedPluginIds = basePlan.LoadedPluginIds,
            HooksBridgeLoaded = basePlan.HooksBridgeLoaded,
            SystemPromptParagraphs = basePlan.SystemPromptParagraphs,
            FirstPromptBlocks = basePlan.FirstPromptBlocks,
            OverlayEntries = basePlan.OverlayEntries,
            EnvironmentRemovals = basePlan.EnvironmentRemovals,
            ProcessWrapper = basePlan.ProcessWrapper,
        };
    }

    private List<string> LaunchArguments(OmpLaunchPlan basePlan, string promptPath, string overlayPath)
    {
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
        return arguments;
    }

    private static Dictionary<string, string> LaunchEnvironment(IReadOnlyDictionary<string, string> contributed, IReadOnlyDictionary<string, string> provider)
    {
        var environment = new Dictionary<string, string>(contributed, StringComparer.Ordinal);
        foreach (var kv in provider)
        {
            environment[kv.Key] = kv.Value;
        }

        return environment;
    }

    private static string? JoinParagraphs(IReadOnlyList<string> paragraphs) => paragraphs.Count == 0 ? null : string.Join('\n', paragraphs);

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
