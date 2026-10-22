using OmpOS.EngineHost;
using OmpOS.Harness.Acp;
using OmpOS.Presentation.Approvals;

namespace OmpOS.Native.Engine;

/// <summary>
/// An engine composed for the native app: the in-process ACP host, and the approval bridge over the permission resolver that host
/// decides requests through. The host starts no process until a prompt is submitted.
/// </summary>
/// <param name="Host">The in-process host that speaks ACP to <c>omp acp</c>.</param>
/// <param name="ApprovalBridge">The bridge that records the user's decisions in the host's permission resolver.</param>
internal sealed record EngineComposition(InProcessEngineHost Host, IToolApprovalBridge ApprovalBridge)
{
    /// <summary>Composes an engine whose approval policy allows exactly <paramref name="allowedKinds"/>. Empty allows nothing.</summary>
    /// <param name="allowedKinds">The tool kinds the approval policy admits.</param>
    /// <returns>The composed engine.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="allowedKinds"/> is null.</exception>
    public static EngineComposition Compose(IReadOnlyCollection<string> allowedKinds)
    {
        ArgumentNullException.ThrowIfNull(allowedKinds);

        var resolver = new PendingPermissionResolver();
        var options = new AcpClientOptions { ApprovalPolicy = new AcpApprovalPolicy(allowedKinds) };
        var host = InProcessEngineHost.ForAcp(options, resolver);
        return new EngineComposition(host, new EngineToolApprovalBridge(resolver, allowedKinds));
    }
}
