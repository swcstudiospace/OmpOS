using Microsoft.Extensions.Logging;
using OmpOS.EngineHost;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
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
}
