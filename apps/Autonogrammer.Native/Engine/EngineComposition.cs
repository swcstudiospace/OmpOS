using Autonogrammer.EngineHost;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Safety;
using Autonogrammer.Harness.Remote;
using Autonogrammer.Presentation.Approvals;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Engine;

/// <summary>
/// An engine composed for the native app: the in-process ACP host, the permission resolver that host decides requests through, and the
/// approval bridge over that resolver. The host starts no process until a prompt is submitted.
/// </summary>
/// <param name="Host">The in-process host that speaks ACP to <c>omp acp</c>.</param>
/// <param name="ApprovalBridge">The bridge that records the user's decisions in <paramref name="Resolver"/>.</param>
/// <param name="Resolver">The resolver that holds the requests the host parked and waits on their decisions.</param>
/// <param name="Reconnecting">The host that keeps a session on an ssh host alive across a lost connection and wraps <paramref name="Host"/>, or null for a local engine. A prompt is sent through it, so a drop is retried and reported.</param>
/// <param name="Remote">The resources of the ssh host the session runs on (its remote file system and convention discovery), or null for a local engine. The owner disposes them after the host.</param>
internal sealed record EngineComposition(InProcessEngineHost Host, IToolApprovalBridge ApprovalBridge, PendingPermissionResolver Resolver, ReconnectingEngineHost? Reconnecting = null, IEnvironmentSessionResources? Remote = null)
{
    /// <summary>Composes an engine whose approval policy allows exactly <paramref name="allowedKinds"/>. Empty allows nothing.</summary>
    /// <param name="allowedKinds">The tool kinds the approval policy admits.</param>
    /// <param name="transportFactory">Creates the transport for each session. Null spawns <c>omp acp</c> in the working directory.</param>
    /// <param name="loggerFactory">Creates the loggers of the host's sessions and transports. Null discards their log output.</param>
    /// <returns>The composed engine.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="allowedKinds"/> is null.</exception>
    public static EngineComposition Compose(IReadOnlyCollection<string> allowedKinds, Func<AcpClientOptions, IAcpTransport>? transportFactory = null, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(allowedKinds);

        var resolver = new PendingPermissionResolver();
        var options = new AcpClientOptions { ApprovalPolicy = new AcpApprovalPolicy(allowedKinds), AuthMethodId = AcpClientOptions.OmpAuthMethodId, HandshakeTimeout = AcpClientOptions.OmpHandshakeTimeout };
        var host = InProcessEngineHost.ForAcp(options, resolver, transportFactory: transportFactory, loggerFactory: loggerFactory);
        return new EngineComposition(host, new EngineToolApprovalBridge(resolver, allowedKinds), resolver);
    }

    /// <summary>
    /// Composes an engine for one session from its launch plan: the plan's working directory, extra <c>omp</c> arguments, environment
    /// and MCP servers go to the ACP client, and the approval policy allows exactly <paramref name="allowedKinds"/> (every kind in the run mode
    /// <see cref="RunMode.RunEverything"/>). The host starts no process until a prompt is submitted, and then resumes <paramref name="resumeSessionId"/> when one is given.
    /// </summary>
    /// <remarks>
    /// With a run mode policy the permission requests the ACP session admits go to <see cref="RunModePermissionResolver"/> first: a call the policy denies or allows is settled there, and only a call it asks about
    /// reaches the resolver the user's decision settles. Hooks decide before either, inside omp and through the hook gate.
    /// </remarks>
    /// <param name="allowedKinds">The tool kinds the approval policy admits. Empty allows nothing.</param>
    /// <param name="plan">How omp is launched for the session.</param>
    /// <param name="transportFactory">Creates the transport for each session. Null spawns <c>omp acp</c> with the plan's arguments and environment. A session on an ssh host connects through its own factory instead.</param>
    /// <param name="loggerFactory">Creates the loggers of the host's sessions and transports. Null discards their log output.</param>
    /// <param name="ompPath">The omp executable, or null to resolve <c>omp</c> from <c>PATH</c>.</param>
    /// <param name="resumeSessionId">The omp session the first prompt resumes with <c>session/load</c>, or null to create a new session.</param>
    /// <param name="options">The hook gate, the run mode, the MCP servers of the user's own files and the remote resources, or null for none.</param>
    /// <returns>The composed engine.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="allowedKinds"/> or <paramref name="plan"/> is null.</exception>
    public static EngineComposition Compose(
        IReadOnlyCollection<string> allowedKinds,
        OmpLaunchPlan plan,
        Func<AcpClientOptions, IAcpTransport>? transportFactory = null,
        ILoggerFactory? loggerFactory = null,
        string? ompPath = null,
        string? resumeSessionId = null,
        EngineCompositionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(allowedKinds);
        ArgumentNullException.ThrowIfNull(plan);

        options ??= new EngineCompositionOptions();
        var kinds = options.RunModePolicy is { } current && current().Mode == RunMode.RunEverything ? RunModeLaunchMapper.AcpToolKinds : allowedKinds;
        var approvalPolicy = new AcpApprovalPolicy(kinds);
        var pending = new PendingPermissionResolver();
        IPermissionResolver resolver = options.RunModePolicy is { } policy
            ? new RunModePermissionResolver(
                pending,
                policy,
                ceiling: request => approvalPolicy.Allows(new AcpPermissionRequest(request.CallId, request.Name, RunModeRequestParser.AcpKind(request), request.PayloadJson)),
                audit: options.RunModeAudit,
                clock: options.Clock)
            : pending;

        var clientOptions = new AcpClientOptions
        {
            ApprovalPolicy = approvalPolicy,
            AuthMethodId = AcpClientOptions.OmpAuthMethodId,
            HandshakeTimeout = AcpClientOptions.OmpHandshakeTimeout,
            OmpPath = ompPath,
            WorkingDirectory = plan.WorkingDirectory,
            ExtraArguments = plan.ExtraArguments,
            EnvironmentVariables = plan.Environment,
            HookGate = options.HookGate,
            McpServers = MergeServers(plan, options.ExtraMcpServers),
        };
        var remote = options.Remote;
        if (remote is not null)
        {
            clientOptions = remote.ApplyTo(clientOptions);
            transportFactory = remote.TransportFactory;
        }

        var host = InProcessEngineHost.ForAcp(clientOptions, resolver, transportFactory: transportFactory, loggerFactory: loggerFactory, resumeSessionId: resumeSessionId, resumeAfterFault: remote is not null);
        var bridge = new EngineToolApprovalBridge(pending, kinds);
        if (remote is null)
        {
            return new EngineComposition(host, bridge, pending, null, null);
        }

        return new EngineComposition(host, bridge, pending, null, remote) { Reconnecting = new ReconnectingEngineHost(host, clock: options.Clock) };
    }

    /// <summary>Joins the plan's HTTP servers and the servers of the user's own files. The plan wins a name clash, because the ACP names of one session must be unique.</summary>
    private static List<AcpMcpServer> MergeServers(OmpLaunchPlan plan, IReadOnlyList<AcpMcpServer> extra)
    {
        var servers = new List<AcpMcpServer>(plan.McpServers.Count + extra.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var server in plan.McpServers)
        {
            _ = names.Add(server.Name);
            servers.Add(new AcpHttpMcpServer(server.Name, server.Url, server.Headers));
        }

        foreach (var server in extra)
        {
            if (names.Add(server.Name))
            {
                servers.Add(server);
            }
        }

        return servers;
    }
}
