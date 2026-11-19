using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Hub-side command states. The hub never marks <see cref="Completed"/> on its own. Wire names come from <see cref="DispatchCommandStateWire"/>.</summary>
[JsonConverter(typeof(DispatchCommandStateJsonConverter))]
public enum DispatchCommandState
{
    /// <summary>Accepted into the per-device queue and not yet written to a socket.</summary>
    Queued,

    /// <summary>Written to the device socket.</summary>
    Delivered,

    /// <summary>The device persisted the command.</summary>
    Received,

    /// <summary>A local approval prompt is open.</summary>
    AwaitingApproval,

    /// <summary>Policy passed and any required approval was granted.</summary>
    Accepted,

    /// <summary>The device is executing the command.</summary>
    Running,

    /// <summary>The device reported completion. The hub does not set this itself.</summary>
    Completed,

    /// <summary>The command failed.</summary>
    Failed,

    /// <summary>The command was cancelled.</summary>
    Cancelled,

    /// <summary>The delivery deadline passed before the device received the command.</summary>
    Expired,
}
