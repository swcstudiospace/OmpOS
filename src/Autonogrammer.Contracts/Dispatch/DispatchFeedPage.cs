namespace Autonogrammer.Contracts.Dispatch;

/// <summary>A page of the account feed.</summary>
/// <param name="Resync">True when the requested position is older than retained history. The caller emits one resync event, then <paramref name="Events"/>.</param>
/// <param name="Events">Events with identifiers greater than the requested position, in order. Empty when the caller is caught up.</param>
public sealed record DispatchFeedPage(bool Resync, IReadOnlyList<DispatchFeedEventRecord> Events);
