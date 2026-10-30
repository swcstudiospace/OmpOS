namespace Autonogrammer.Contracts.Dispatch;

/// <summary>One account-feed event on <c>GET /dispatch/events</c>.</summary>
public sealed record DispatchFeedEventDto
{
    /// <summary>Monotonic account feed identifier.</summary>
    public required long Id { get; init; }

    /// <summary>Event kind. <c>resync</c> means the requested position is gone.</summary>
    public required string Kind { get; init; }

    /// <summary>Event time, Unix seconds.</summary>
    public required long At { get; init; }

    /// <summary>JSON payload. Never a command body.</summary>
    public string? Payload { get; init; }
}
