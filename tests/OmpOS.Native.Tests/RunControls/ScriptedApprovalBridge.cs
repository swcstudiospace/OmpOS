using OmpOS.Presentation.Approvals;

namespace OmpOS.Native.Tests.RunControls;

/// <summary>An approval bridge that answers each decision with the next scripted outcome and records every call.</summary>
internal sealed class ScriptedApprovalBridge : IToolApprovalBridge
{
    private readonly Queue<ToolApprovalOutcome> _outcomes;

    /// <summary>Creates a bridge that answers with <paramref name="outcomes"/> in order.</summary>
    /// <param name="outcomes">The outcomes, one per decision.</param>
    public ScriptedApprovalBridge(params ToolApprovalOutcome[] outcomes)
    {
        _outcomes = new Queue<ToolApprovalOutcome>(outcomes);
    }

    /// <summary>Gets every decision the bridge received, in order.</summary>
    public List<ApprovalCall> Calls { get; } = [];

    /// <inheritdoc/>
    public Task<ToolApprovalOutcome> DecideAsync(string sessionId, string requestId, ToolApprovalDecision decision, CancellationToken cancellationToken)
    {
        Calls.Add(new ApprovalCall(sessionId, requestId, decision));
        return Task.FromResult(_outcomes.Dequeue());
    }
}
