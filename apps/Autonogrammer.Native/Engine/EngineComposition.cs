using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.EngineHost;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Safety;
using Autonogrammer.Harness.Remote;
using Autonogrammer.Harness.Rpc;
using Autonogrammer.Presentation.Approvals;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Engine;

/// <summary>
/// An engine composed for the native app: the in-process host, the permission resolver that host decides requests through, and the
/// approval bridge over that resolver. The host starts no process until a prompt is submitted. On the RPC path the resolver and
/// bridge are present but never fed: RPC sessions cannot reach them, because omp RPC has no observed permission surface (OQ-525)
/// and no RPC code path may grant a permission.
/// </summary>
/// <param name="Host">The in-process host that speaks to omp over the composed transport (ACP, unless RPC was selected).</param>
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
    /// Composes an engine for one session from its launch plan over the transport <paramref name="options"/> selects (ACP by
    /// default). On the ACP path the plan's working directory, extra <c>omp</c> arguments, environment and MCP servers go to the
    /// ACP client, and the approval policy allows exactly <paramref name="allowedKinds"/> (every kind in the run mode
    /// <see cref="RunMode.RunEverything"/>). The host starts no process until a prompt is submitted, and then resumes
    /// <paramref name="resumeSessionId"/> when one is given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With a run mode policy the permission requests the ACP session admits go to <see cref="RunModePermissionResolver"/> first: a call the policy denies or allows is settled there, and only a call it asks about
    /// reaches the resolver the user's decision settles. Hooks decide before either, inside omp and through the hook gate.
    /// A <see cref="EngineCompositionOptions.PermissionResolverFactory"/> wraps the ask-the-user resolver instead. It cannot be combined with a run mode policy.
    /// </para>
    /// <para>
    /// With <see cref="EngineCompositionOptions.Transport"/> set to <see cref="EngineTransport.Rpc"/> the engine is composed over
    /// <see cref="InProcessEngineHost.ForRpc"/> for a plain local session instead. The composition carries the same
    /// <see cref="PendingPermissionResolver"/> and approval bridge objects, but RPC sessions cannot reach them — omp RPC has no
    /// observed permission-request surface (OQ-525), so they are never fed and no RPC path grants a permission. What RPC cannot
    /// yet honour is refused rather than dropped: a remote session (Phase 83); an approval-capable flow, that is non-empty
    /// <paramref name="allowedKinds"/>, a <see cref="EngineCompositionOptions.RunModePolicy"/> or a
    /// <see cref="EngineCompositionOptions.PermissionResolverFactory"/> (OQ-525); and the launch contributions omp RPC has no
    /// observed channel for — plan or extra MCP servers, overlay entries with no settings overlay file, a hook gate or a
    /// process wrapper (OQ-526). The plan's extra arguments, including <c>--config</c>, reach RPC byte-for-byte through
    /// <see cref="OmpRpcClientOptions.ExtraArguments"/>. <paramref name="transportFactory"/> and
    /// <paramref name="loggerFactory"/> configure ACP sessions only and are not consulted on the RPC path;
    /// <see cref="EngineCompositionOptions.RpcSessionFactory"/> is its scripted-test seam.
    /// </para>
    /// </remarks>
    /// <param name="allowedKinds">The tool kinds the approval policy admits. Empty allows nothing. Must be empty on the RPC path (OQ-525).</param>
    /// <param name="plan">How omp is launched for the session.</param>
    /// <param name="transportFactory">Creates the transport for each ACP session. Null spawns <c>omp acp</c> with the plan's arguments and environment. A session on an ssh host connects through its own factory instead. Not consulted on the RPC path.</param>
    /// <param name="loggerFactory">Creates the loggers of the host's ACP sessions and transports. Null discards their log output. Not consulted on the RPC path.</param>
    /// <param name="ompPath">The omp executable, or null to resolve <c>omp</c> from <c>PATH</c>.</param>
    /// <param name="resumeSessionId">
    /// The omp session the first prompt resumes, or null to create a new session: an ACP session identifier on the ACP path
    /// (resumed with <c>session/load</c>), and a session file path on the RPC path (resumed with <c>switch_session</c>). The two
    /// are not interchangeable — a persisted ACP identifier must not be passed when the transport is RPC.
    /// </param>
    /// <param name="options">The transport selection, the hook gate, the run mode, the MCP servers of the user's own files and the remote resources, or null for none (ACP).</param>
    /// <returns>The composed engine.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="allowedKinds"/> or <paramref name="plan"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="EngineCompositionOptions.Transport"/> is not a defined value.</exception>
    /// <exception cref="ArgumentException">
    /// <see cref="EngineCompositionOptions.PermissionResolverFactory"/> is set together with <see cref="EngineCompositionOptions.RunModePolicy"/>, or the factory returns null;
    /// or, on the RPC path, the composition carries a remote session (Phase 83), an approval-capable flow (OQ-525) or a launch
    /// contribution RPC has no observed channel for (OQ-526) — see the remarks.
    /// </exception>
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
        if (!Enum.IsDefined(options.Transport))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Transport, "The engine transport is not a defined value.");
        }

        if (options.Transport == EngineTransport.Rpc)
        {
            return ComposeRpc(allowedKinds, plan, ompPath, resumeSessionId, options);
        }

        return ComposeAcp(allowedKinds, plan, transportFactory, loggerFactory, ompPath, resumeSessionId, options);
    }

    /// <summary>Composes the ACP path of the plan-based <see cref="Compose(IReadOnlyCollection{string}, OmpLaunchPlan, Func{AcpClientOptions, IAcpTransport}, ILoggerFactory, string, string, EngineCompositionOptions)"/>.</summary>
    private static EngineComposition ComposeAcp(
        IReadOnlyCollection<string> allowedKinds,
        OmpLaunchPlan plan,
        Func<AcpClientOptions, IAcpTransport>? transportFactory,
        ILoggerFactory? loggerFactory,
        string? ompPath,
        string? resumeSessionId,
        EngineCompositionOptions options)
    {
        if (options.PermissionResolverFactory is not null && options.RunModePolicy is not null)
        {
            throw new ArgumentException("PermissionResolverFactory cannot be combined with RunModePolicy.", nameof(options));
        }

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
        if (options.PermissionResolverFactory is { } wrap)
        {
            resolver = wrap(pending) ?? throw new ArgumentException("PermissionResolverFactory returned null.", nameof(options));
        }

        var clientOptions = new AcpClientOptions
        {
            ApprovalPolicy = approvalPolicy,
            AuthMethodId = AcpClientOptions.OmpAuthMethodId,
            HandshakeTimeout = AcpClientOptions.OmpHandshakeTimeout,
            OmpPath = ompPath,
            WorkingDirectory = plan.WorkingDirectory,
            ExtraArguments = plan.ExtraArguments,
            EnvironmentVariables = plan.Environment,
            EnvironmentMode = options.EnvironmentMode,
            EnvironmentAllowList = options.EnvironmentAllowList,
            HookGate = options.HookGate,
            McpServers = MergeServers(plan, options.ExtraMcpServers),
            ProcessWrapper = options.ProcessWrapper,
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

    /// <summary>
    /// Composes a plain local session over RPC, refusing every combination RPC cannot yet honour instead of silently dropping it.
    /// The returned composition carries the same fresh <see cref="PendingPermissionResolver"/> and bridge as the ACP path, but no
    /// RPC session can reach them, so they are never fed (OQ-525). <paramref name="resumeSessionId"/> is an RPC session file path
    /// on this path, never a persisted ACP identifier.
    /// </summary>
    private static EngineComposition ComposeRpc(IReadOnlyCollection<string> allowedKinds, OmpLaunchPlan plan, string? ompPath, string? resumeSessionId, EngineCompositionOptions options)
    {
        if (options.Remote is not null)
        {
            throw new ArgumentException("The RPC engine transport cannot compose a session on a remote host; remote sessions stay on ACP until Phase 83.", nameof(options));
        }

        if (allowedKinds.Count != 0)
        {
            throw new ArgumentException("The RPC engine transport cannot compose an approval-capable flow: omp RPC has no observed permission surface and stays deny-by-default, so sessions that admit tool kinds stay on ACP (OQ-525).", nameof(allowedKinds));
        }

        if (options.RunModePolicy is not null || options.PermissionResolverFactory is not null)
        {
            throw new ArgumentException("The RPC engine transport cannot compose an approval-capable flow (run mode or permission resolver): omp RPC has no observed permission surface and stays deny-by-default, so such sessions stay on ACP (OQ-525).", nameof(options));
        }

        if (plan.McpServers.Count != 0)
        {
            throw new ArgumentException("The RPC engine transport has no observed channel for plan MCP servers (OQ-526); sessions that carry MCP servers stay on ACP.", nameof(plan));
        }

        if (plan.OverlayEntries.Count != 0 && plan.ConfigOverlayPath is null)
        {
            throw new ArgumentException("The RPC engine transport cannot honour overlay entries with no settings overlay file on the plan; the caller writes the overlay before composing (OQ-526).", nameof(plan));
        }

        if (options.ExtraMcpServers.Count != 0)
        {
            throw new ArgumentException("The RPC engine transport has no observed channel for user MCP servers (OQ-526); sessions that carry MCP servers stay on ACP.", nameof(options));
        }

        if (options.HookGate is not null)
        {
            throw new ArgumentException("The RPC engine transport cannot honour a hook gate (OQ-526); sessions with hooks stay on ACP.", nameof(options));
        }

        if (options.ProcessWrapper is not null)
        {
            throw new ArgumentException("The RPC engine transport cannot honour a process wrapper (OQ-526); wrapped sessions stay on ACP.", nameof(options));
        }

        var rpcOptions = new OmpRpcClientOptions
        {
            Cwd = plan.WorkingDirectory,
            OmpPath = ompPath ?? "omp",
            EnvironmentVariables = ResolveRpcEnvironment(plan, options),
            ExtraArguments = plan.ExtraArguments,
        };
        var pending = new PendingPermissionResolver();
        var bridge = new EngineToolApprovalBridge(pending, allowedKinds);
        return options.RpcSessionFactory is { } rpcSessionFactory
            ? ComposeRpcOverSeam(rpcSessionFactory, rpcOptions, resumeSessionId, bridge, pending)
            : new EngineComposition(InProcessEngineHost.ForRpc(new OmpRpcClient(rpcOptions), resumeSessionId: resumeSessionId), bridge, pending, null, null);
    }

    /// <summary>
    /// Composes the RPC engine over the scripted-test seam, reporting the same in-process descriptor
    /// <see cref="InProcessEngineHost.ForRpc"/> defaults to. The composition owns the host, exactly as on the ACP path.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: the returned composition owns the host and its owner disposes it after the session (SessionSlot), exactly as the ACP factories' hosts are owned.")]
    private static EngineComposition ComposeRpcOverSeam(
        Func<OmpRpcClientOptions, IOmpSession> rpcSessionFactory,
        OmpRpcClientOptions rpcOptions,
        string? resumeSessionId,
        EngineToolApprovalBridge bridge,
        PendingPermissionResolver pending) =>
        new(
            new InProcessEngineHost(
                () => rpcSessionFactory(rpcOptions) ?? throw new InvalidOperationException("RpcSessionFactory returned null."),
                new EngineHostDescriptor { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None },
                resumeSessionId),
            bridge,
            pending,
            null,
            null);

    /// <summary>
    /// Resolves the complete environment of the RPC child. <see cref="OmpRpcClientOptions.EnvironmentVariables"/> is replacement
    /// semantics (null inherits unchanged, a dictionary is the whole environment), while the plan's environment is an overlay on
    /// top of the inherited one, so composition applies the same <see cref="EngineEnvironmentPolicy"/> the ACP transport applies:
    /// the inherited variables the policy admits, overlaid with the plan's.
    /// </summary>
    private static Dictionary<string, string>? ResolveRpcEnvironment(OmpLaunchPlan plan, EngineCompositionOptions options)
    {
        if (options.EnvironmentMode == EngineEnvironmentMode.Inherit && plan.Environment.Count == 0)
        {
            return null;
        }

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var environment = new Dictionary<string, string?>(comparer);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            environment[(string)entry.Key] = (string?)entry.Value;
        }

        new EngineEnvironmentPolicy(options.EnvironmentMode, options.EnvironmentAllowList).Apply(environment, plan.Environment);
        var resolved = new Dictionary<string, string>(environment.Count, comparer);
        foreach (var (name, value) in environment)
        {
            if (value is not null)
            {
                resolved[name] = value;
            }
        }

        return resolved;
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
