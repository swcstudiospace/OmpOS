using Autonogrammer.Presentation.Approvals;

namespace Autonogrammer.Native.Tests.RunControls;

/// <summary>One decision that reached the approval bridge.</summary>
/// <param name="SessionId">The session that raised the request.</param>
/// <param name="RequestId">The request the decision answers.</param>
/// <param name="Decision">The decision.</param>
internal sealed record ApprovalCall(string SessionId, string RequestId, ToolApprovalDecision Decision);
