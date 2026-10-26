namespace OmpOS.Harness.Abstractions;

/// <summary>The engine queue a live steering message is put into.</summary>
public enum OmpSteerQueue
{
    /// <summary>The steering queue: the running turn takes the message at its next tool-batch or model-call boundary.</summary>
    Steering,

    /// <summary>The follow-up queue: the running turn takes the message after the work it has already planned.</summary>
    FollowUp,
}
