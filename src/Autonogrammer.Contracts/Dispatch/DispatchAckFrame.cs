namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Device to hub acknowledgement.</summary>
public sealed record DispatchAckFrame
{
    /// <summary>Protocol version. Always 1.</summary>
    public required int V { get; init; }

    /// <summary>Frame type <c>ack</c>.</summary>
    public required string T { get; init; }

    /// <summary>Command identifier.</summary>
    public required string CommandId { get; init; }

    /// <summary>Acknowledgement state wire name, including <c>duplicate</c>.</summary>
    public required string State { get; init; }
}
