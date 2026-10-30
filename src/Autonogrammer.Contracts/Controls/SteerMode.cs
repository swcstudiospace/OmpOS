using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Controls;

/// <summary>How a message sent while a run is active reaches it.</summary>
public enum SteerMode
{
    /// <summary>Hand the message to the active run at once, or hold it until the turn ends when the engine has no live steering.</summary>
    [JsonStringEnumMemberName("send_now")]
    SendNow,

    /// <summary>Queue the message as a follow-up that starts after the active turn.</summary>
    [JsonStringEnumMemberName("queue")]
    Queue,
}
