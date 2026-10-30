namespace Autonogrammer.Contracts.Dispatch;

/// <summary>One account-feed event. Identifiers increase per account, never across accounts.</summary>
/// <param name="Id">Monotonic identifier for the account.</param>
/// <param name="AccountId">Owning account.</param>
/// <param name="Kind">Event kind.</param>
/// <param name="Payload">JSON payload. Never a command body.</param>
/// <param name="At">Event time.</param>
public sealed record DispatchFeedEventRecord(long Id, string AccountId, string Kind, string? Payload, DateTimeOffset At);
