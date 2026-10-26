using OmpOS.Harness.Abstractions;
using OmpOS.Presentation.Approvals;

namespace OmpOS.Native.Engine;

/// <summary>
/// The approval bridge of a composed engine. It settles the parked request in the engine's <see cref="PendingPermissionResolver"/>
/// with the user's decision, and reports what the engine recorded.
/// </summary>
/// <remarks>
/// <para>
/// A deny settles the request as denied and reports <see cref="ToolApprovalOutcome.Accepted"/>. An allow is granted only when the
/// composed policy allows at least one tool kind; otherwise it reports <see cref="ToolApprovalOutcome.Refused"/> and leaves the request
/// pending, so the user may still deny it. The session parks a request only after its policy admits it, so an allow never settles a
/// request the policy forbade.
/// </para>
/// <para>
/// A decision for a request that is no longer pending reports <see cref="ToolApprovalOutcome.AlreadyDecided"/>. One engine
/// composition binds one session, so the session identifier is not used to look up the request.
/// </para>
/// </remarks>
internal sealed class EngineToolApprovalBridge : IToolApprovalBridge
{
    private readonly PendingPermissionResolver _resolver;
    private readonly bool _policyAllowsAnyTool;

    /// <summary>Creates a bridge over <paramref name="resolver"/> for an engine whose policy allows <paramref name="allowedKinds"/>.</summary>
    /// <param name="resolver">The engine's permission resolver, which holds the parked requests.</param>
    /// <param name="allowedKinds">The tool kinds the composed approval policy allows. Empty allows nothing.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public EngineToolApprovalBridge(PendingPermissionResolver resolver, IReadOnlyCollection<string> allowedKinds)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(allowedKinds);

        _resolver = resolver;
        _policyAllowsAnyTool = allowedKinds.Count > 0;
    }

    /// <summary>
    /// Settles the parked request with the user's decision. A deny always settles the request. An allow is gated on the composed policy
    /// allowing at least one tool kind, not on the kind of this request. That is sound because the host parks only the requests its
    /// policy admits, so a non-empty allowed set means the request being decided was admitted. An allow with no admitted kind reports
    /// refused and leaves the request pending, so the user may still deny it.
    /// </summary>
    /// <param name="sessionId">The session that raised the request. The bridge composes one session, so requests are found by id.</param>
    /// <param name="requestId">The identifier of the parked request.</param>
    /// <param name="decision">The user's decision.</param>
    /// <param name="cancellationToken">Not consulted: the settlement completes synchronously.</param>
    /// <returns>The outcome of the decision.</returns>
    public Task<ToolApprovalOutcome> DecideAsync(string sessionId, string requestId, ToolApprovalDecision decision, CancellationToken cancellationToken)
    {
        if (decision == ToolApprovalDecision.Allow && !_policyAllowsAnyTool)
        {
            return Task.FromResult(ToolApprovalOutcome.Refused);
        }

        var permission = decision == ToolApprovalDecision.Allow ? PermissionDecision.Allow : PermissionDecision.Deny;
        var outcome = _resolver.TrySettle(requestId, permission) ? ToolApprovalOutcome.Accepted : ToolApprovalOutcome.AlreadyDecided;
        return Task.FromResult(outcome);
    }
}
