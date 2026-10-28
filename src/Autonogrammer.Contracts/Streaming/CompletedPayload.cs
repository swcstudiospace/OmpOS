namespace Autonogrammer.Contracts.Streaming;

/// <summary>
/// Terminal stream event (kind "completed").
/// </summary>
public sealed record CompletedPayload : StreamEventPayload
{
    /// <summary>Gets the completion reason.</summary>
    public required string Reason { get; init; }
}
