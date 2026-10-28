using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Presentation.Cloud;

/// <summary>
/// What each <see cref="CloudAgentStatus"/> says, shows and allows. The words and the glyph keys are the single source for the Cloud agents
/// page, the details and the rail; the permissions decide which action buttons are enabled so a view never has a dead control.
/// </summary>
public static class CloudAgentStatusMap
{
    /// <summary>Gets every status in life-cycle order, for filters and tests.</summary>
    public static IReadOnlyList<CloudAgentStatus> All { get; } = Enum.GetValues<CloudAgentStatus>();

    /// <summary>Gets the status in words, for example <c>Limit reached</c>. An undefined value reads as <c>Status unknown</c>.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The words.</returns>
    public static string TextOf(CloudAgentStatus status) => status switch
    {
        CloudAgentStatus.Provisioning => CloudCopy.StatusProvisioning,
        CloudAgentStatus.Starting => CloudCopy.StatusStarting,
        CloudAgentStatus.Running => CloudCopy.StatusRunning,
        CloudAgentStatus.Idle => CloudCopy.StatusIdle,
        CloudAgentStatus.Stopping => CloudCopy.StatusStopping,
        CloudAgentStatus.Stopped => CloudCopy.StatusStopped,
        CloudAgentStatus.Failed => CloudCopy.StatusFailed,
        CloudAgentStatus.Deleting => CloudCopy.StatusDeleting,
        CloudAgentStatus.Orphaned => CloudCopy.StatusOrphaned,
        CloudAgentStatus.LimitReached => CloudCopy.StatusLimitReached,
        _ => CloudCopy.StatusUnknown,
    };

    /// <summary>Gets a sentence that explains the status and what the user can do about it.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The sentence.</returns>
    public static string DescriptionOf(CloudAgentStatus status) => status switch
    {
        CloudAgentStatus.Provisioning => CloudCopy.DescribeProvisioning,
        CloudAgentStatus.Starting => CloudCopy.DescribeStarting,
        CloudAgentStatus.Running => CloudCopy.DescribeRunning,
        CloudAgentStatus.Idle => CloudCopy.DescribeIdle,
        CloudAgentStatus.Stopping => CloudCopy.DescribeStopping,
        CloudAgentStatus.Stopped => CloudCopy.DescribeStopped,
        CloudAgentStatus.Failed => CloudCopy.DescribeFailed,
        CloudAgentStatus.Deleting => CloudCopy.DescribeDeleting,
        CloudAgentStatus.Orphaned => CloudCopy.DescribeOrphaned,
        CloudAgentStatus.LimitReached => CloudCopy.DescribeLimitReached,
        _ => CloudCopy.StatusUnknown,
    };

    /// <summary>Gets the glyph key of the status, from <see cref="StatusIcon"/>.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The glyph key.</returns>
    public static string IconOf(CloudAgentStatus status) => status switch
    {
        CloudAgentStatus.Provisioning or CloudAgentStatus.Starting => StatusIcon.Clock,
        CloudAgentStatus.Running => StatusIcon.Running,
        CloudAgentStatus.Idle => StatusIcon.Ring,
        CloudAgentStatus.Stopping => StatusIcon.Paused,
        CloudAgentStatus.Stopped => StatusIcon.Closed,
        CloudAgentStatus.Failed => StatusIcon.Cross,
        CloudAgentStatus.Deleting => StatusIcon.Slash,
        CloudAgentStatus.Orphaned => StatusIcon.Question,
        CloudAgentStatus.LimitReached => StatusIcon.Alert,
        _ => StatusIcon.Info,
    };

    /// <summary>Gets whether the status is a passing state that ends by itself: provisioning, starting, stopping or deleting.</summary>
    /// <param name="status">The status.</param>
    /// <returns>True for a transitional status.</returns>
    public static bool IsTransitional(CloudAgentStatus status) =>
        status is CloudAgentStatus.Provisioning or CloudAgentStatus.Starting or CloudAgentStatus.Stopping or CloudAgentStatus.Deleting;

    /// <summary>Gets whether a worker in this status can be woken: only a stopped worker.</summary>
    /// <param name="status">The status.</param>
    /// <returns>True when Wake is offered.</returns>
    public static bool CanWake(CloudAgentStatus status) => status == CloudAgentStatus.Stopped;

    /// <summary>Gets whether a worker in this status can be stopped: starting, running or idle.</summary>
    /// <param name="status">The status.</param>
    /// <returns>True when Stop is offered.</returns>
    public static bool CanStop(CloudAgentStatus status) =>
        status is CloudAgentStatus.Starting or CloudAgentStatus.Running or CloudAgentStatus.Idle;

    /// <summary>Gets whether an agent in this status can be deleted: every status except one that is already being deleted.</summary>
    /// <param name="status">The status.</param>
    /// <returns>True when Delete is offered.</returns>
    public static bool CanDelete(CloudAgentStatus status) => Enum.IsDefined(status) && status != CloudAgentStatus.Deleting;

    /// <summary>Gets whether a message can be sent to an agent in this status: running or idle.</summary>
    /// <param name="status">The status.</param>
    /// <returns>True when sending is offered.</returns>
    public static bool CanPrompt(CloudAgentStatus status) => status is CloudAgentStatus.Running or CloudAgentStatus.Idle;

    /// <summary>Gets whether the agent's session can be opened in Agents: not while provisioning or deleting, and not for an orphan.</summary>
    /// <param name="status">The status.</param>
    /// <returns>True when Open in Agents is offered.</returns>
    public static bool CanOpenInAgents(CloudAgentStatus status) =>
        Enum.IsDefined(status) && status is not (CloudAgentStatus.Provisioning or CloudAgentStatus.Deleting or CloudAgentStatus.Orphaned);

    /// <summary>Gets whether a pull request can be created from an agent in this status: the worker holds the branch, so any status but provisioning, deleting and orphaned.</summary>
    /// <param name="status">The status.</param>
    /// <returns>True when Create pull request is offered.</returns>
    public static bool CanCreatePullRequest(CloudAgentStatus status) => CanOpenInAgents(status);
}
