using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Closed command operations for relay protocol version 1. There is no <c>computer.*</c> operation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DispatchOp>))]
public enum DispatchOp
{
    /// <summary>Lists sessions on the desktop.</summary>
    [JsonStringEnumMemberName("session.list")]
    SessionList,

    /// <summary>Steers an existing session.</summary>
    [JsonStringEnumMemberName("session.steer")]
    SessionSteer,

    /// <summary>Cancels an existing session.</summary>
    [JsonStringEnumMemberName("session.cancel")]
    SessionCancel,

    /// <summary>Decides a pending approval. A decision of kind <c>computer</c> is refused by the device.</summary>
    [JsonStringEnumMemberName("approval.decide")]
    ApprovalDecide,

    /// <summary>Starts or continues a code session.</summary>
    [JsonStringEnumMemberName("code.prompt")]
    CodePrompt,

    /// <summary>Starts a cowork task. Tool classes are payload, not operations.</summary>
    [JsonStringEnumMemberName("cowork.task")]
    CoworkTask,

    /// <summary>Cancels a command on the same device.</summary>
    [JsonStringEnumMemberName("command.cancel")]
    CommandCancel,
}
