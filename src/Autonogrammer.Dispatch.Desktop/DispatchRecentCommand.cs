namespace Autonogrammer.Dispatch.Desktop;

/// <summary>One applied command kept in the dedupe window.</summary>
/// <param name="CommandId">The command identifier.</param>
/// <param name="Nonce">The delivery nonce.</param>
public sealed record DispatchRecentCommand(string CommandId, string Nonce);
