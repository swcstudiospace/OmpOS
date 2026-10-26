namespace OmpOS.Presentation.AgentPanel;

/// <summary>What the input channel did with an accepted steering entry. The panel never claims more than the channel reported.</summary>
public enum SteeringDeliveryStatus
{
    /// <summary>The message waits in the queue and starts after the active turn: a follow-up, or a steer that queued behind one.</summary>
    QueuedAfterTurn = 0,

    /// <summary>The engine has no steer command and holds the message until the active turn ends.</summary>
    HeldUntilTurnEnds = 1,

    /// <summary>The engine took the steering message at once.</summary>
    Delivered = 2,
}
