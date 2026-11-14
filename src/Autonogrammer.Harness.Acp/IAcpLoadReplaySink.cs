namespace Autonogrammer.Harness.Acp;

/// <summary>
/// Receives the history an agent replays while a session is loaded with <c>session/load</c>. The agent sends that history as
/// <c>session/update</c> notifications before it answers the load; with no prompt in flight they would otherwise be discarded.
/// </summary>
public interface IAcpLoadReplaySink
{
    /// <summary>
    /// Receives the replay of one load. It runs once, after the load answered, on the task that opened the session. An exception
    /// fails the open: a consumer that cannot record what the agent replayed must not let the session proceed as if it had.
    /// </summary>
    /// <param name="replay">The replayed updates, in wire order, and whether they are complete.</param>
    void LoadReplayed(AcpLoadReplay replay);
}
