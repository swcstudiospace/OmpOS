namespace Autonogrammer.Cowork.Abstractions;

/// <summary>An ask shown to a person.</summary>
/// <param name="SessionId">The session id.</param>
/// <param name="CallId">The call id.</param>
/// <param name="ToolName">The tool name.</param>
/// <param name="Preview">The host-observed preview.</param>
/// <param name="Origin">The turn origin.</param>
/// <param name="MustBeLocal">Whether a decision from another device is refused.</param>
public sealed record CoworkApprovalRequest(
    string SessionId,
    string CallId,
    string ToolName,
    CoworkCallPreview Preview,
    CoworkTurnOrigin Origin,
    bool MustBeLocal);
