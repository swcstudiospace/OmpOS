namespace OmpOS.Contracts.Streaming;

/// <summary>
/// A stream error (kind "error").
/// </summary>
public sealed record ErrorPayload : StreamEventPayload
{
    /// <summary>Gets the error message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets whether the failed operation may be retried.</summary>
    public required bool Retryable { get; init; }
}
