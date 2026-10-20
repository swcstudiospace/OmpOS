namespace OmpOS.Contracts.Sessions;

/// <summary>
/// A single chat message inside a session.
/// </summary>
public sealed record Message
{
    /// <summary>Gets the message role (for example "user" or "assistant").</summary>
    public required string Role { get; init; }

    /// <summary>Gets the message content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets the timestamp the message was recorded.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
