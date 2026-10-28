using Autonogrammer.Contracts.Streaming;

namespace Autonogrammer.Contracts.Sessions;

/// <summary>
/// An agent session: ordered messages plus the append-only stream-event log.
/// </summary>
public sealed record Session
{
    /// <summary>Gets the session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the owner identifier: the subject of the bearer token that created the session.</summary>
    public required string OwnerId { get; init; }

    /// <summary>Gets the creation timestamp.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the last-update timestamp.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the session lifecycle state.</summary>
    public required SessionStatus Status { get; init; }

    /// <summary>Gets the sandbox identifier, or null when the session runs without a sandbox.</summary>
    public string? SandboxId { get; init; }

    /// <summary>Gets the ordered chat messages.</summary>
    public required IReadOnlyList<Message> Messages { get; init; }

    /// <summary>Gets the append-only stream-event log.</summary>
    public required IReadOnlyList<StreamEvent> Events { get; init; }
}
