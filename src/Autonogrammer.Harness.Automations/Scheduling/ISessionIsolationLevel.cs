#pragma warning disable MA0048 // The plan owns this file for the isolation seam and its default.

namespace Autonogrammer.Harness.Automations.Scheduling;

/// <summary>How isolated the session a scheduled task would start is. Phase 76 replaces the default.</summary>
public enum SessionIsolationLevel
{
    /// <summary>The session is not filesystem-isolated. Skip is refused.</summary>
    NotIsolated,

    /// <summary>The session is filesystem-isolated. Skip may be saved and started.</summary>
    FilesystemIsolated,
}

/// <summary>
/// Reports the isolation level the validator and the runner consult. The engine does not decide isolation itself.
/// </summary>
public interface ISessionIsolationLevel
{
    /// <summary>Gets the level in force. The default reports <see cref="SessionIsolationLevel.NotIsolated"/> until phase 76 lands.</summary>
    SessionIsolationLevel Level { get; }
}

/// <summary>The level in force until phase 76 supplies a real reporter. Skip tasks are refused end to end.</summary>
public sealed class DefaultSessionIsolation : ISessionIsolationLevel
{
    /// <summary>Gets the shared default.</summary>
    public static DefaultSessionIsolation Instance { get; } = new();

    /// <inheritdoc />
    public SessionIsolationLevel Level => SessionIsolationLevel.NotIsolated;
}
