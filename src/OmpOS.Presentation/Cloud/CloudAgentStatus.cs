namespace OmpOS.Presentation.Cloud;

/// <summary>
/// Where a cloud agent is in its life, in words the user can act on. The host maps the control plane's own states onto these ten (for
/// example Cloning onto <see cref="Provisioning"/>, Waking onto <see cref="Starting"/>, Sleeping onto <see cref="Stopped"/>); an agent the
/// host cannot place at all is <see cref="Orphaned"/>.
/// </summary>
public enum CloudAgentStatus
{
    /// <summary>A worker is being set up and the repositories are being cloned. Nothing can be sent yet.</summary>
    Provisioning = 0,

    /// <summary>The worker is waking up or booting; the agent will be reachable shortly.</summary>
    Starting = 1,

    /// <summary>The agent is working on a task.</summary>
    Running = 2,

    /// <summary>The worker is awake and waiting for a message. It goes to sleep after the idle time.</summary>
    Idle = 3,

    /// <summary>The worker is being put to sleep.</summary>
    Stopping = 4,

    /// <summary>The worker is asleep. Its saved files are kept; waking it continues from them.</summary>
    Stopped = 5,

    /// <summary>The agent ended with an error.</summary>
    Failed = 6,

    /// <summary>The worker and its files are being removed.</summary>
    Deleting = 7,

    /// <summary>The service no longer knows the worker, so only deleting the record is possible.</summary>
    Orphaned = 8,

    /// <summary>A limit (concurrent agents, awake agents or quota) stopped the agent from continuing.</summary>
    LimitReached = 9,
}
