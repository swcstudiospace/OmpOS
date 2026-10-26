namespace OmpOS.Native.Views.AgentPanel;

/// <summary>The arguments of <see cref="AgentRailView.CloseRequested"/>: the agent row the request is about.</summary>
internal sealed class AgentRowEventArgs : EventArgs
{
    /// <summary>Creates the arguments for one agent row.</summary>
    /// <param name="sessionId">The session identifier of the row.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sessionId"/> is null.</exception>
    public AgentRowEventArgs(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        SessionId = sessionId;
    }

    /// <summary>Gets the session identifier of the row.</summary>
    public string SessionId { get; }
}
