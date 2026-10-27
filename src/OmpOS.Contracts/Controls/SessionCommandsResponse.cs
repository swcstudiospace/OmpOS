namespace OmpOS.Contracts.Controls;

/// <summary>The answer of <c>GET /sessions/{id}/commands</c>.</summary>
public sealed record SessionCommandsResponse
{
    /// <summary>Gets the session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets a value indicating whether the engine advertised commands for the session. When false, <see cref="Commands"/> is empty and <see cref="Reason"/> says why.</summary>
    public required bool Available { get; init; }

    /// <summary>Gets a value indicating whether the commands come from a live engine session or from the last list an earlier run advertised.</summary>
    public required bool Live { get; init; }

    /// <summary>Gets a plain sentence that says why the commands are unavailable or not live, or null.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the commands, in the engine's order.</summary>
    public required IReadOnlyList<SessionCommand> Commands { get; init; }
}
