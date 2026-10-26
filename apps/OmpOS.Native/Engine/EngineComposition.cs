using Microsoft.Extensions.Logging;
using OmpOS.EngineHost;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Presentation.Approvals;

namespace OmpOS.Native.Engine;

/// <summary>
/// An engine composed for the native app: the in-process ACP host, the permission resolver that host decides requests through, and the
/// approval bridge over that resolver. The host starts no process until a prompt is submitted.
/// </summary>
/// <param name="Host">The in-process host that speaks ACP to <c>omp acp</c>.</param>
/// <param name="ApprovalBridge">The bridge that records the user's decisions in <paramref name="Resolver"/>.</param>
/// <param name="Resolver">The resolver that holds the requests the host parked and waits on their decisions.</param>
internal sealed record EngineComposition(InProcessEngineHost Host, IToolApprovalBridge ApprovalBridge, PendingPermissionResolver Resolver)
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
        var options = new AcpClientOptions { ApprovalPolicy = new AcpApprovalPolicy(allowedKinds) };
        var host = InProcessEngineHost.ForAcp(options, resolver, transportFactory: transportFactory, loggerFactory: loggerFactory);
        return new EngineComposition(host, new EngineToolApprovalBridge(resolver, allowedKinds), resolver);
    }

    /// <summary>
    /// Composes an engine for one session from its launch plan: the plan's working directory, extra <c>omp</c> arguments, environment
    /// and MCP servers go to the ACP client, and the approval policy allows exactly <paramref name="allowedKinds"/>. The host starts no
    /// process until a prompt is submitted, and then resumes <paramref name="resumeSessionId"/> when one is given.
    /// </summary>
    /// <param name="allowedKinds">The tool kinds the approval policy admits. Empty allows nothing.</param>
    /// <param name="plan">How omp is launched for the session.</param>
    /// <param name="transportFactory">Creates the transport for each session. Null spawns <c>omp acp</c> with the plan's arguments and environment.</param>
    /// <param name="loggerFactory">Creates the loggers of the host's sessions and transports. Null discards their log output.</param>
    /// <param name="ompPath">The omp executable, or null to resolve <c>omp</c> from <c>PATH</c>.</param>
    /// <param name="resumeSessionId">The omp session the first prompt resumes with <c>session/load</c>, or null to create a new session.</param>
    /// <returns>The composed engine.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="allowedKinds"/> or <paramref name="plan"/> is null.</exception>
    public static EngineComposition Compose(
        IReadOnlyCollection<string> allowedKinds,
        OmpLaunchPlan plan,
        Func<AcpClientOptions, IAcpTransport>? transportFactory = null,
        ILoggerFactory? loggerFactory = null,
        string? ompPath = null,
        string? resumeSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(allowedKinds);
        ArgumentNullException.ThrowIfNull(plan);

        var resolver = new PendingPermissionResolver();
        var options = new AcpClientOptions
        {
            ApprovalPolicy = new AcpApprovalPolicy(allowedKinds),
            OmpPath = ompPath,
            WorkingDirectory = plan.WorkingDirectory,
            ExtraArguments = plan.ExtraArguments,
            EnvironmentVariables = plan.Environment,
            McpServers = [.. plan.McpServers.Select(static server => new AcpHttpMcpServer(server.Name, server.Url, server.Headers))],
        };
        var host = InProcessEngineHost.ForAcp(options, resolver, transportFactory: transportFactory, loggerFactory: loggerFactory, resumeSessionId: resumeSessionId);
        return new EngineComposition(host, new EngineToolApprovalBridge(resolver, allowedKinds), resolver);
    }
}
