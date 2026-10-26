namespace OmpOS.Harness.Herdr;

/// <summary>
/// The lifecycle state of one herdr session as the server reports it. Attachment is a separate fact, carried by
/// <see cref="HerdrSessionSnapshot.IsAttached"/>, because detaching a client never stops the agent.
/// </summary>
public enum HerdrSessionState
{
    /// <summary>The agent process is running in its pane.</summary>
    Running,

    /// <summary>The agent is paused on request. Its process is still alive.</summary>
    Paused,

    /// <summary>The session was ended on purpose. It cannot run again.</summary>
    Terminated,

    /// <summary>
    /// The server restarted and the agent process is gone. The record keeps its native reference, so the conversation can
    /// resume through a create request that carries that reference.
    /// </summary>
    Lost,
}
