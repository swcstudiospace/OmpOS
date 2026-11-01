using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Presentation.Approvals;

namespace Autonogrammer.Native.Cowork;

/// <summary>Completes a Cowork ask from the local approval card. It is not the hosted tool-approval endpoint.</summary>
internal sealed class CoworkApprovalBridge(CoworkApprovalBroker broker) : IToolApprovalBridge
{
    /// <inheritdoc />
    public Task<ToolApprovalOutcome> DecideAsync(string sessionId, string requestId, ToolApprovalDecision decision, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        var outcome = decision == ToolApprovalDecision.Allow ? CoworkApprovalOutcome.Allowed : CoworkApprovalOutcome.Denied;
        return Task.FromResult(broker.CompleteLocal(requestId, outcome) ? ToolApprovalOutcome.Accepted : ToolApprovalOutcome.AlreadyDecided);
    }
}
