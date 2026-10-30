namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Device to hub progress event. Text is at most 1,024 characters.</summary>
public sealed record DispatchEvtFrame
{
    /// <summary>Protocol version. Always 1.</summary>
    public required int V { get; init; }

    /// <summary>Frame type <c>evt</c>.</summary>
    public required string T { get; init; }

    /// <summary>Command identifier.</summary>
    public required string CommandId { get; init; }

    /// <summary>Event kind, such as <c>approval_pending</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Redacted text, at most 1,024 characters.</summary>
    public string? Text { get; init; }
}
