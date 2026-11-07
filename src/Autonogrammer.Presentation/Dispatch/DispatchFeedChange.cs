namespace Autonogrammer.Presentation.Dispatch;

/// <summary>A change to one command, as the feed reports it.</summary>
/// <param name="CommandId">The command.</param>
/// <param name="Phase">The command's phase now.</param>
public sealed record DispatchFeedChange(string CommandId, DispatchPhase Phase);
