namespace Autonogrammer.UI.Components.Account;

/// <summary>One message on the auth channel. <paramref name="Kind"/> is <c>code</c> or <c>ack</c>.</summary>
/// <param name="Kind">The message kind.</param>
/// <param name="Code">The callback code, or null for an acknowledgement.</param>
public sealed record AuthChannelEnvelope(string Kind, string? Code);
