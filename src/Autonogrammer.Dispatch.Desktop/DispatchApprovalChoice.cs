namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The local user's answer to a dispatched Computer Use task. A remote decision is never an input here.</summary>
public enum DispatchApprovalChoice
{
    /// <summary>The user denied the task, or the dialog did not answer. Deny is the default.</summary>
    Deny = 0,

    /// <summary>The user allowed this task once.</summary>
    ApproveOnce = 1,

    /// <summary>The user denied the task and paused Dispatch on this desktop.</summary>
    DenyAndPause = 2,
}
