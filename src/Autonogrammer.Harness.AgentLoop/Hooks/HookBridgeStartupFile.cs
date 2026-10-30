namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>
/// The startup file the server writes for the extension: which events need a decision from the host and which of them must
/// be denied when the host cannot be reached. The names on the wire are camelCase and the lists are sorted.
/// </summary>
internal sealed record HookBridgeStartupFile
{
    /// <summary>The schema version this build writes.</summary>
    internal const int CurrentVersion = 1;

    /// <summary>The time added to the handler timeout to get <see cref="RequestTimeoutMs"/>: 10 s of slack for the answer to travel.</summary>
    internal const int RequestTimeoutMarginMs = 10_000;

    /// <summary>Gets the schema version.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Gets the events for which an unreachable or failing bridge must deny the action.</summary>
    public required IReadOnlyList<string> FailClosedEvents { get; init; }

    /// <summary>Gets the Cursor events that have at least one enabled hook.</summary>
    public required IReadOnlyList<string> ActiveEvents { get; init; }

    /// <summary>Gets the omp event names that have hooks in the <c>omp</c> section.</summary>
    public required IReadOnlyList<string> ActiveOmpEvents { get; init; }

    /// <summary>
    /// Gets how long the extension may wait for an answer to one POST, in milliseconds: the handler timeout plus
    /// <see cref="RequestTimeoutMarginMs"/>, so the server's own timeout always answers before the extension gives up.
    /// </summary>
    public required int RequestTimeoutMs { get; init; }
}
