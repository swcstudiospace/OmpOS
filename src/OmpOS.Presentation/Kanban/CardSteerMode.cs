namespace OmpOS.Presentation.Kanban;

/// <summary>How a message sent from a card reaches its session.</summary>
public enum CardSteerMode
{
    /// <summary>Deliver the message into the running turn now (the Steer intent).</summary>
    SendNow,

    /// <summary>Queue the message for the end of the current turn (the follow-up intent).</summary>
    FollowUp,
}
