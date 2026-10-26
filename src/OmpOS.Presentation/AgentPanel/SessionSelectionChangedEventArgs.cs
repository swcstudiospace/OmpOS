namespace OmpOS.Presentation.AgentPanel;

/// <summary>Carries the session a selection or close request names.</summary>
public sealed class SessionSelectionChangedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="sessionId">The session, or null when the selection was cleared.</param>
    public SessionSelectionChangedEventArgs(string? sessionId)
    {
        SessionId = sessionId;
    }

    /// <summary>Gets the session identifier, or null when the selection was cleared.</summary>
    public string? SessionId { get; }
}
