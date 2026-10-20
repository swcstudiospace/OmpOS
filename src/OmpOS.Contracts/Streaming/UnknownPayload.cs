using System.Text.Json;

namespace OmpOS.Contracts.Streaming;

/// <summary>
/// Fallback for unknown or missing payload <c>kind</c> discriminators.
/// The received payload is preserved verbatim for byte-identical re-serialization.
/// </summary>
public sealed record UnknownPayload : StreamEventPayload
{
    /// <summary>Gets the unrecognized kind string, or null when no kind was present.</summary>
    public string? OriginalKind { get; init; }

    /// <summary>Gets the received payload verbatim.</summary>
    public required JsonElement Raw { get; init; }
}
