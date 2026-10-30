using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Cloud;

/// <summary>Where a cloud agent is in its life. The wire value is the lower-case member name, with an underscore in <see cref="LimitReached"/>.</summary>
public enum CloudAgentStatus
{
    /// <summary>The control plane is creating or claiming the worker and its volume.</summary>
    [JsonStringEnumMemberName("provisioning")]
    Provisioning,

    /// <summary>The worker is waking after an idle period or a failed start; no prompt can run yet.</summary>
    [JsonStringEnumMemberName("starting")]
    Starting,

    /// <summary>The worker is awake and the agent is active: a prompt is running or the idle window has not elapsed.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>The agent is logically idle. The worker may sleep; a prompt or a wake request starts it again.</summary>
    [JsonStringEnumMemberName("idle")]
    Idle,

    /// <summary>A stop request is being carried out.</summary>
    [JsonStringEnumMemberName("stopping")]
    Stopping,

    /// <summary>The agent was stopped by its owner. A wake request is needed before a prompt.</summary>
    [JsonStringEnumMemberName("stopped")]
    Stopped,

    /// <summary>Provisioning or starting failed. <see cref="CloudAgentResponse.Error"/> says why; a wake request retries.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>The agent is being deleted: its worker and volume are being removed.</summary>
    [JsonStringEnumMemberName("deleting")]
    Deleting,

    /// <summary>The record names a worker that no longer exists, or the worker could not be matched to the record.</summary>
    [JsonStringEnumMemberName("orphaned")]
    Orphaned,

    /// <summary>The control plane stopped the agent because it used its compute-second budget.</summary>
    [JsonStringEnumMemberName("limit_reached")]
    LimitReached,
}
