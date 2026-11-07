namespace Autonogrammer.Cowork.Abstractions;

/// <summary>One audit record. It holds no arguments and no result bytes.</summary>
/// <param name="SessionId">The session id.</param>
/// <param name="CallId">The call id.</param>
/// <param name="ProviderId">The provider id.</param>
/// <param name="ToolName">The tool name.</param>
/// <param name="Origin">The turn origin from the lease.</param>
/// <param name="Policy">The gate outcome, or deny when the call never reached the gate.</param>
/// <param name="Approval">The broker outcome, or null when the host did not ask.</param>
/// <param name="IsError">Whether the result the model reads is an error.</param>
/// <param name="ResultKinds">The kinds present in the result before a cap failure replaced it.</param>
/// <param name="Duration">How long the sequence took.</param>
/// <param name="Target">The host-observed target, or null.</param>
public sealed record CoworkToolAuditEntry(
    string SessionId,
    string CallId,
    string ProviderId,
    string ToolName,
    CoworkTurnOrigin Origin,
    CoworkPolicyOutcome Policy,
    CoworkApprovalOutcome? Approval,
    bool IsError,
    CoworkResultKinds ResultKinds,
    TimeSpan Duration,
    string? Target);
