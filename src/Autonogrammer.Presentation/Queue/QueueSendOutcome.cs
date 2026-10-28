namespace Autonogrammer.Presentation.Queue;

/// <summary>The result of sending a queued message now.</summary>
public enum QueueSendOutcome
{
    /// <summary>The message was delivered to the running turn and left the queue.</summary>
    Sent = 0,

    /// <summary>No run is active any more, so nothing was sent and the message stays in the queue; send it as a new prompt.</summary>
    NoActiveRun = 1,

    /// <summary>The delivery failed or was refused. The message is back in its place in the queue.</summary>
    Failed = 2,

    /// <summary>The queue has no such message.</summary>
    NotFound = 3,
}
