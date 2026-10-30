namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Device to hub terminal result. Text is at most 4,096 characters.</summary>
public sealed record DispatchResFrame
{
    /// <summary>Protocol version. Always 1.</summary>
    public required int V { get; init; }

    /// <summary>Frame type <c>res</c>.</summary>
    public required string T { get; init; }

    /// <summary>Command identifier.</summary>
    public required string CommandId { get; init; }

    /// <summary>Terminal state wire name.</summary>
    public required string State { get; init; }

    /// <summary>Closed failure reason, when the state is failed.</summary>
    public string? Reason { get; init; }

    /// <summary>Redacted text, at most 4,096 characters.</summary>
    public string? Text { get; init; }
}
