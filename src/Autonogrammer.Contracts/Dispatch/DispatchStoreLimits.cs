namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Caps the store enforces. Routes map the refusals; the numbers live here.</summary>
/// <param name="MaxQueuedPerDevice">Queued commands allowed on one device. Default 100.</param>
/// <param name="DevicesPerAccount">Non-revoked devices allowed on one account. Default 20.</param>
/// <param name="FeedRetention">How long account-feed events are kept. Default 30 days.</param>
/// <param name="BodyRetention">How long a terminal command body is kept. Default 7 days.</param>
public sealed record DispatchStoreLimits(int MaxQueuedPerDevice, int DevicesPerAccount, TimeSpan FeedRetention, TimeSpan BodyRetention)
{
    /// <summary>The documented defaults.</summary>
    public static DispatchStoreLimits Default { get; } = new(100, 20, TimeSpan.FromDays(30), TimeSpan.FromDays(7));
}
