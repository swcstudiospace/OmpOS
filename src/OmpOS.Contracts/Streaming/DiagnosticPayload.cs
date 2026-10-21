namespace OmpOS.Contracts.Streaming;

/// <summary>
/// A non-terminal diagnostic (kind "diagnostic"). The stream and the turn continue after it.
/// </summary>
public sealed record DiagnosticPayload : StreamEventPayload
{
    /// <summary>Gets the human-readable diagnostic message.</summary>
    public required string Message { get; init; }
}
