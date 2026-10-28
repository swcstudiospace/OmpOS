using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Catalog;

/// <summary>The status a catalog row shows. It combines the stored session status, the closed flag and the staleness of a running session; the members mirror the desktop row statuses.</summary>
public enum CatalogRowStatus
{
    /// <summary>A tool approval waits for the user.</summary>
    [JsonStringEnumMemberName("needs-approval")]
    NeedsApproval,

    /// <summary>The agent waits for the user's next message.</summary>
    [JsonStringEnumMemberName("waiting-for-you")]
    WaitingForYou,

    /// <summary>A run is executing.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>A run was accepted and has not started.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>The session shows as running but no run has touched it for longer than a run lease lives.</summary>
    [JsonStringEnumMemberName("lost")]
    Lost,

    /// <summary>The session exists and has not run yet.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,

    /// <summary>The last run was cancelled.</summary>
    [JsonStringEnumMemberName("paused")]
    Paused,

    /// <summary>The operator closed the session.</summary>
    [JsonStringEnumMemberName("closed")]
    Closed,

    /// <summary>The last run finished successfully.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,

    /// <summary>The last run failed.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>The status is not known to this version.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}
