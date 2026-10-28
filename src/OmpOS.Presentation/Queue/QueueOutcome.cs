namespace OmpOS.Presentation.Queue;

/// <summary>The result of adding a message to the queue.</summary>
public enum QueueOutcome
{
    /// <summary>The message is in the queue.</summary>
    Queued = 0,

    /// <summary>The text was blank, so nothing happened.</summary>
    Ignored = 1,

    /// <summary>The queue holds the most messages it may; the draft should be kept.</summary>
    Full = 2,
}
