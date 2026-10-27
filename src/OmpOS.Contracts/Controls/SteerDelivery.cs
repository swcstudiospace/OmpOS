using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Controls;

/// <summary>What the channel did with an accepted steering message. The API never claims more than the engine or the store proved.</summary>
public enum SteerDelivery
{
    /// <summary>The engine took the message into the running turn.</summary>
    [JsonStringEnumMemberName("delivered")]
    Delivered,

    /// <summary>The message waits in the session queue as a follow-up.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>The engine has no live steering for this session; the message is recorded and held until the turn ends.</summary>
    [JsonStringEnumMemberName("held")]
    Held,

    /// <summary>The message did not reach the session. The reason says why.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
