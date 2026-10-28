using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// Adapts a session's dispatcher to the loopback bridge: each request the omp extension posts runs the hooks of its event, and the merged
/// decision goes back in the bridge's wire form. A <c>sessionStart</c> decision's <c>additional_context</c> is kept for the next prompt, because
/// omp has no hook to put it in front of the first prompt itself.
/// </summary>
internal sealed class HookBridgeAdapter(HooksSession session) : IHookBridgeHandler
{
    /// <inheritdoc/>
    public async ValueTask<BridgeDecision> HandleAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var isOmp = string.Equals(request.Kind, BridgeRequest.OmpKind, StringComparison.Ordinal);
        var decision = await session.DispatchNamedAsync(request.Event, isOmp, request.Payload, request.ToolCallId, cancellationToken).ConfigureAwait(false);
        return ToBridge(decision);
    }

    /// <summary>Converts a merged decision to the wire form the extension reads.</summary>
    /// <param name="decision">The decision.</param>
    /// <returns>The bridge decision. A decision with nothing in it is an allow with nothing to do.</returns>
    internal static BridgeDecision ToBridge(HookDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var wire = decision.ToWire();
        return new BridgeDecision
        {
            Permission = wire.Permission,
            UserMessage = wire.UserMessage,
            AgentMessage = wire.AgentMessage,
            UpdatedInput = wire.UpdatedInput,
            AdditionalContext = wire.AdditionalContext,
            FollowupMessage = wire.FollowupMessage,
            Continue = wire.Continue,
            UpdatedMcpToolOutput = wire.UpdatedMcpToolOutput,
            Env = wire.Env,
            OmpResult = wire.OmpResult,
            HooksRun = wire.HooksRun,
        };
    }
}
