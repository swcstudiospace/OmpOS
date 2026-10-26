namespace OmpOS.Presentation.Kanban;

/// <summary>What happened to a message sent from a card.</summary>
public enum CardSteerOutcome
{
    /// <summary>The session took the message into its running turn.</summary>
    Delivered,

    /// <summary>The message waits for the end of the turn.</summary>
    Queued,

    /// <summary>The message was not accepted; the session is unchanged.</summary>
    Failed,
}
