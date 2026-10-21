namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Log cursor for streaming.
/// </summary>
public sealed record LogCursor
{
    /// <summary>Gets the starting position (byte offset or sequence number).</summary>
    public required long Position { get; init; }

    /// <summary>Gets whether to follow (stream new logs).</summary>
    public required bool Follow { get; init; }
}
