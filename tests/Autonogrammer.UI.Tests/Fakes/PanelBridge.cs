using Autonogrammer.Presentation.Approvals;

namespace Autonogrammer.UI.Tests.Fakes;

/// <summary>The permission bridge behind the approval rows: each test scripts the answer, and the bridge records what was decided.</summary>
internal sealed class PanelBridge : IToolApprovalBridge
{
    /// <summary>Gets or sets the answer of the next decision; the default accepts.</summary>
    internal Func<ToolApprovalDecision, CancellationToken, Task<ToolApprovalOutcome>> Answer { get; set; } =
        static (_, _) => Task.FromResult(ToolApprovalOutcome.Accepted);

    /// <summary>Gets the decisions received, as <c>requestId:decision</c>.</summary>
    internal List<string> Decisions { get; } = [];

    /// <inheritdoc />
    public Task<ToolApprovalOutcome> DecideAsync(string sessionId, string requestId, ToolApprovalDecision decision, CancellationToken cancellationToken)
    {
        Decisions.Add(requestId + ":" + decision);
        return Answer(decision, cancellationToken);
    }
}
