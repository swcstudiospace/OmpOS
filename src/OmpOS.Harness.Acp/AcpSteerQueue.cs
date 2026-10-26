namespace OmpOS.Harness.Acp;

/// <summary>The omp queue a <c>_session/steering</c> request puts its message into.</summary>
public enum AcpSteerQueue
{
    /// <summary>The steering queue (the default): the running turn takes the message at its next tool-batch or model-call boundary.</summary>
    Steering,

    /// <summary>The follow-up queue, selected with <c>_meta.steering.queue = "followUp"</c>: the running turn takes the message after the work it has already planned.</summary>
    FollowUp,
}
