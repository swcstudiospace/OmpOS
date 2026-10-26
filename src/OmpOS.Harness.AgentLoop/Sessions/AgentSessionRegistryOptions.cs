namespace OmpOS.Harness.AgentLoop.Sessions;

/// <summary>Options of <see cref="AgentSessionRegistry"/>.</summary>
public sealed record AgentSessionRegistryOptions
{
    /// <summary>Gets how long a running session may go without a heartbeat before <see cref="AgentSessionRegistry.DetectLost"/> marks it lost. The default is 45 seconds.</summary>
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Gets the directory under which each session gets <c>sessions/&lt;id&gt;</c> as its state directory, or null to use the directory
    /// of the state file.
    /// </summary>
    public string? StateRoot { get; init; }
}
