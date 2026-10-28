using System.Globalization;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Launch;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Rewrites an <see cref="OmpLaunchPlan"/> made for this machine into the launch of omp on a remote machine. What cannot exist there is
/// handled in one of three ways, and every degradation is reported as a <see cref="StageEvent"/> so the user sees it:
/// <list type="bullet">
/// <item><description>Local plugin paths (<c>-e</c>): rewritten when a <see cref="RemotePluginMapping"/> says where the plugin is installed on the
/// remote machine; otherwise left out. OmpOS does not copy plugins to the remote machine; the session runs without them.</description></item>
/// <item><description>The settings overlay (<c>--config</c>): its content is written to the remote session directory from the ssh stdin and
/// the argument points at the remote file.</description></item>
/// <item><description>Services on this machine's loopback (the MCP gateway or the substrate): the address is rewritten to a remote loopback port
/// and a reverse port forward (<c>ssh -R</c>) carries the traffic back. Other local-only values (<c>HOME</c>, the XDG directories) are dropped.</description></item>
/// </list>
/// Provider tokens are given as a separate dictionary and are never in an argument: they become part of the environment that travels in the
/// stdin preamble (see <see cref="SshCommandBuilder"/>).
/// </summary>
public sealed class RemoteLaunchMaterializer
{
    private const string RemoteStageId = "remote-launch";
    private const string OverlayFileName = "omp-overlay.yml";
    private const long MaxOverlayBytes = 256 * 1024;

    private static readonly HashSet<string> LocalOnlyEnvironment = new(StringComparer.Ordinal)
    {
        "HOME",
        "PI_CODING_AGENT_DIR",
        "XDG_CONFIG_HOME",
        "XDG_DATA_HOME",
        "XDG_CACHE_HOME",
        "XDG_STATE_HOME",
        "SUBSTRATE_TOKENS_DIR",
    };

    private static readonly HashSet<string> LoopbackHosts = new(StringComparer.Ordinal) { "127.0.0.1", "localhost", "[::1]" };

    private readonly TimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="RemoteLaunchMaterializer"/> class.</summary>
    /// <param name="clock">The clock that stamps the stage events, or <see langword="null"/> for the system clock.</param>
    public RemoteLaunchMaterializer(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    /// <summary>Makes the remote launch.</summary>
    /// <param name="plan">The launch plan made for this machine.</param>
    /// <param name="profile">The remote machine. It must pass <see cref="EnvironmentProfileValidator"/>.</param>
    /// <param name="options">The session identifier, the remote home directory and the local state root.</param>
    /// <param name="secretEnvironment">
    /// Environment variables whose values are secrets (provider tokens), or <see langword="null"/>. They are delivered through the stdin preamble
    /// and are never in an argument; the launch lists their names only.
    /// </param>
    /// <returns>The remote launch.</returns>
    /// <exception cref="ArgumentException">An input is not valid, or the settings overlay file named by the plan is missing or too large.</exception>
    public RemoteLaunch Materialize(
        OmpLaunchPlan plan,
        SshProfile profile,
        RemoteLaunchOptions options,
        IReadOnlyDictionary<string, string>? secretEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(options);
        Validate(profile, options);

        var remoteState = options.RemoteHomeDirectory.TrimEnd('/') + "/.ompos/sessions/" + options.SessionId;
        var localSession = Path.TrimEndingDirectorySeparator(Path.GetFullPath(SessionStateLayout.SessionDirectory(options.LocalStateRoot, options.SessionId)));
        var forwards = new ReverseForwardAllocator(options.SessionId, options.ReversePortBase, options.ReversePortSpan, options.CarryLoopbackServices);
        var events = new List<StageEvent>(plan.PlanningEvents);
        var files = new List<RemoteFile>();

        var remoteOverlay = remoteState + "/" + OverlayFileName;
        var arguments = RewriteArguments(plan, options, profile, remoteOverlay, events, out var droppedPluginEntries);
        if (plan.ConfigOverlayPath is { } overlayPath)
        {
            files.Add(new RemoteFile(remoteOverlay, ReadOverlay(overlayPath)));
        }

        var environment = RewriteEnvironment(plan, localSession, remoteState, forwards, options, events);
        var secretNames = new HashSet<string>(StringComparer.Ordinal);
        if (secretEnvironment is not null)
        {
            foreach (var (name, value) in secretEnvironment)
            {
                environment[name] = value;
                _ = secretNames.Add(name);
            }
        }

        var servers = RewriteMcpServers(plan, forwards, options, events);
        return new RemoteLaunch
        {
            WorkingDirectory = profile.RemoteWorkspaceRoot,
            OmpPath = profile.RemoteOmpPath ?? "omp",
            Arguments = arguments,
            StateDirectory = remoteState,
            Environment = environment,
            SecretEnvironmentNames = secretNames,
            Files = files,
            ReverseForwards = forwards.Forwards,
            McpServers = servers,
            PlanningEvents = events,
            LoadedPluginIds = droppedPluginEntries > 0 ? [] : plan.LoadedPluginIds,
        };
    }

    private static void Validate(SshProfile profile, RemoteLaunchOptions options)
    {
        var issues = EnvironmentProfileValidator.Validate(profile);
        if (issues.Count > 0)
        {
            throw new ArgumentException("The ssh profile is not valid.", nameof(profile));
        }

        if (!SessionStateLayout.IsValidSessionId(options.SessionId))
        {
            throw new ArgumentException("The session id is not a safe path segment.", nameof(options));
        }

        if (!SshProfileRules.IsValidAbsoluteRemotePath(options.RemoteHomeDirectory) || string.IsNullOrWhiteSpace(options.LocalStateRoot))
        {
            throw new ArgumentException("The remote home directory must be an absolute remote path and the local state root must be set.", nameof(options));
        }

        if (options.ReversePortBase < 1024 || options.ReversePortSpan < 1 || options.ReversePortBase + options.ReversePortSpan - 1 > 65535)
        {
            throw new ArgumentException("The reverse forward port range must lie between 1024 and 65535.", nameof(options));
        }

        foreach (var mapping in options.PluginMappings)
        {
            if (!Path.IsPathFullyQualified(mapping.LocalRoot) || !SshProfileRules.IsValidAbsoluteRemotePath(mapping.RemoteRoot))
            {
                throw new ArgumentException("A plugin mapping needs an absolute local directory and an absolute remote directory.", nameof(options));
            }
        }
    }

    private static string ReadOverlay(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxOverlayBytes)
            {
                throw new ArgumentException("The settings overlay file the plan names does not exist or is larger than 256 KiB.", nameof(path));
            }

            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException("The settings overlay file the plan names could not be read.", nameof(path), ex);
        }
    }

    private static string? MapPlugin(string localPath, IReadOnlyList<RemotePluginMapping> mappings)
    {
        var full = Path.GetFullPath(localPath);
        foreach (var mapping in mappings)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(mapping.LocalRoot));
            if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
                return mapping.RemoteRoot.TrimEnd('/') + "/" + relative;
            }
        }

        return null;
    }

    private static string? Relocate(string value, string localSession, string remoteState)
    {
        if (!Path.IsPathRooted(value))
        {
            return null;
        }

        var full = Path.GetFullPath(value);
        if (string.Equals(full, localSession, StringComparison.Ordinal))
        {
            return remoteState;
        }

        return full.StartsWith(localSession + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? remoteState + "/" + Path.GetRelativePath(localSession, full).Replace('\\', '/')
            : null;
    }

    private static bool TryRewriteLoopbackUrl(string text, ReverseForwardAllocator forwards, out string rewritten, out bool hasUserInfo)
    {
        rewritten = text;
        hasUserInfo = false;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            || !LoopbackHosts.Contains(uri.Host))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            hasUserInfo = true;
            return false;
        }

        if (!forwards.Enabled)
        {
            return false;
        }

        var remotePort = forwards.PortFor(uri.Host, uri.Port);
        var authorityStart = text.IndexOf("://", StringComparison.Ordinal) + 3;
        var restStart = text.IndexOfAny(['/', '?', '#'], authorityStart);
        var remainder = restStart < 0 ? string.Empty : text[restStart..];
        rewritten = string.Create(CultureInfo.InvariantCulture, $"{uri.Scheme}://127.0.0.1:{remotePort}{remainder}");
        return true;
    }

    private List<string> RewriteArguments(
        OmpLaunchPlan plan,
        RemoteLaunchOptions options,
        SshProfile profile,
        string remoteOverlay,
        List<StageEvent> events,
        out int droppedPluginEntries)
    {
        var arguments = new List<string>(plan.ExtraArguments.Count);
        droppedPluginEntries = 0;
        var droppedConfigs = 0;
        for (var index = 0; index < plan.ExtraArguments.Count; index++)
        {
            var argument = plan.ExtraArguments[index];
            var hasValue = index + 1 < plan.ExtraArguments.Count;
            if (string.Equals(argument, "-e", StringComparison.Ordinal))
            {
                if (!hasValue)
                {
                    droppedPluginEntries++;
                    continue;
                }

                index++;
                if (MapPlugin(plan.ExtraArguments[index], options.PluginMappings) is { } remotePath)
                {
                    arguments.Add("-e");
                    arguments.Add(remotePath);
                }
                else
                {
                    droppedPluginEntries++;
                }
            }
            else if (string.Equals(argument, "--config", StringComparison.Ordinal))
            {
                if (!hasValue)
                {
                    droppedConfigs++;
                    continue;
                }

                index++;
                if (plan.ConfigOverlayPath is not null && string.Equals(plan.ExtraArguments[index], plan.ConfigOverlayPath, StringComparison.Ordinal))
                {
                    arguments.Add("--config");
                    arguments.Add(remoteOverlay);
                }
                else
                {
                    droppedConfigs++;
                }
            }
            else
            {
                arguments.Add(argument);
            }
        }

        AddDegradedEvents(options, profile, events, droppedPluginEntries, droppedConfigs);

        return arguments;
    }

    private void AddDegradedEvents(RemoteLaunchOptions options, SshProfile profile, List<StageEvent> events, int droppedPluginEntries, int droppedConfigs)
    {
        if (droppedPluginEntries > 0)
        {
            events.Add(Degraded(
                options.SessionId,
                RemoteStageId + ":plugins",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{droppedPluginEntries} plugin entry file(s) cannot run on {profile.Alias}: their directories are not installed there, and OmpOS does not copy plugins. The session runs without them. Install the plugins on the remote machine and map the local directory to the remote one to use them.")));
        }

        if (droppedConfigs > 0)
        {
            events.Add(Degraded(options.SessionId, RemoteStageId + ":config", "A settings file named on the local command line does not exist on the remote machine and was left out."));
        }
    }

    private Dictionary<string, string> RewriteEnvironment(
        OmpLaunchPlan plan,
        string localSession,
        string remoteState,
        ReverseForwardAllocator forwards,
        RemoteLaunchOptions options,
        List<StageEvent> events)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        var dropped = new List<string>();
        foreach (var (name, value) in plan.Environment)
        {
            if (LocalOnlyEnvironment.Contains(name))
            {
                dropped.Add(name);
            }
            else if (TryRewriteLoopbackUrl(value, forwards, out var rewritten, out var hasUserInfo))
            {
                environment[name] = rewritten;
            }
            else if (hasUserInfo)
            {
                dropped.Add(name);
            }
            else if (Relocate(value, localSession, remoteState) is { } relocated)
            {
                environment[name] = relocated;
            }
            else
            {
                environment[name] = value;
            }
        }

        if (dropped.Count > 0)
        {
            dropped.Sort(StringComparer.Ordinal);
            events.Add(Degraded(
                options.SessionId,
                RemoteStageId + ":environment",
                "These local-only environment variables were not sent to the remote machine: " + string.Join(", ", dropped) + "."));
        }

        return environment;
    }

    private List<AcpHttpMcpServer> RewriteMcpServers(OmpLaunchPlan plan, ReverseForwardAllocator forwards, RemoteLaunchOptions options, List<StageEvent> events)
    {
        var servers = new List<AcpHttpMcpServer>(plan.McpServers.Count);
        foreach (var spec in plan.McpServers)
        {
            var address = spec.Url.OriginalString;
            if (TryRewriteLoopbackUrl(address, forwards, out var rewritten, out var hasUserInfo))
            {
                servers.Add(new AcpHttpMcpServer(spec.Name, new Uri(rewritten, UriKind.Absolute), new Dictionary<string, string>(spec.Headers, StringComparer.Ordinal)));
                continue;
            }

            if (hasUserInfo)
            {
                events.Add(Degraded(options.SessionId, RemoteStageId + ":mcp:" + spec.Name, "The MCP server '" + spec.Name + "' has user information in its address and was left out of the remote session."));
                continue;
            }

            servers.Add(new AcpHttpMcpServer(spec.Name, spec.Url, new Dictionary<string, string>(spec.Headers, StringComparer.Ordinal)));
            if (spec.Url.IsLoopback)
            {
                events.Add(Degraded(options.SessionId, RemoteStageId + ":mcp:" + spec.Name, "The MCP server '" + spec.Name + "' uses a loopback address that cannot be forwarded; it is unlikely to be reachable from the remote machine."));
            }
        }

        return servers;
    }

    private StageEvent Degraded(string sessionId, string stageId, string detail) =>
        new()
        {
            SessionId = sessionId,
            Source = StageSource.Omp,
            StageId = stageId,
            Title = "Remote launch",
            Status = StageStatus.Degraded,
            Timestamp = _clock.GetUtcNow(),
            Detail = detail,
        };
}
