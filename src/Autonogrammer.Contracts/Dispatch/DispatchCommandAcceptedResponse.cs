namespace Autonogrammer.Contracts.Dispatch;

/// <summary>202 body of an accepted or replayed command. The replay returns this same shape and the same command id.</summary>
public sealed record DispatchCommandAcceptedResponse
{
    /// <summary>Command identifier.</summary>
    public required string CommandId { get; init; }

    /// <summary>Per-device sequence.</summary>
    public required long Seq { get; init; }

    /// <summary>Hub state wire name at accept time.</summary>
    public required string State { get; init; }
}
