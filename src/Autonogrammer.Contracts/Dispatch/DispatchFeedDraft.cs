namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// A feed event to append in the same transaction as a command transition. A transition that does not apply appends nothing.
/// </summary>
/// <param name="Kind">The event kind, for example <c>command.state</c>.</param>
/// <param name="Payload">The JSON payload, or null.</param>
public sealed record DispatchFeedDraft(string Kind, string? Payload);
