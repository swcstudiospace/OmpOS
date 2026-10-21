namespace OmpOS.Contracts.Streaming;

/// <summary>
/// Incremental text output (kind "text_delta").
/// </summary>
public sealed record TextDeltaPayload : StreamEventPayload
{
    /// <summary>Gets the text delta.</summary>
    public required string Delta { get; init; }
}
