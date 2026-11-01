using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Hub-side command states. The hub never marks <see cref="Completed"/> on its own.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DispatchCommandState>))]
public enum DispatchCommandState
{
    /// <summary>Accepted into the per-device queue and not yet written to a socket.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>Written to the device socket.</summary>
    [JsonStringEnumMemberName("delivered")]
    Delivered,

    /// <summary>The device persisted the command.</summary>
    [JsonStringEnumMemberName("received")]
    Received,

    /// <summary>A local approval prompt is open.</summary>
    [JsonStringEnumMemberName("awaiting_approval")]
    AwaitingApproval,

    /// <summary>Policy passed and any required approval was granted.</summary>
    [JsonStringEnumMemberName("accepted")]
    Accepted,

    /// <summary>The device is executing the command.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>The device reported completion. The hub does not set this itself.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,

    /// <summary>The command failed.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>The command was cancelled.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    /// <summary>The delivery deadline passed before the device received the command.</summary>
    [JsonStringEnumMemberName("expired")]
    Expired,
}
