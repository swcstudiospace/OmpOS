namespace Autonogrammer.Cowork.Abstractions;

/// <summary>What the gate needs in order to decide.</summary>
/// <param name="Session">The session.</param>
/// <param name="Tool">The descriptor.</param>
/// <param name="Invocation">The call.</param>
/// <param name="Preview">The host-observed preview.</param>
/// <param name="SessionTainted">Whether the session is tainted.</param>
public sealed record CoworkPolicyRequest(
    CoworkSessionContext Session,
    CoworkToolDescriptor Tool,
    CoworkToolInvocation Invocation,
    CoworkCallPreview Preview,
    bool SessionTainted);
