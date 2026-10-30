namespace Autonogrammer.Cowork.Abstractions;

/// <summary>A host event. It carries no arguments and no result bytes.</summary>
/// <param name="SessionId">The session id.</param>
/// <param name="CallId">The call id.</param>
/// <param name="ProviderId">The provider id.</param>
/// <param name="ToolName">The tool name.</param>
/// <param name="Kind">The point reached.</param>
/// <param name="Target">The host-observed target, or null.</param>
public sealed record CoworkToolEvent(
    string SessionId,
    string CallId,
    string ProviderId,
    string ToolName,
    CoworkToolEventKind Kind,
    string? Target);
