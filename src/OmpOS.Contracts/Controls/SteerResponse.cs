using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Controls;

/// <summary>The answer of <c>POST /sessions/{id}/steer</c>.</summary>
public sealed record SteerResponse
{
    /// <summary>Gets what the channel did with the message.</summary>
    public required SteerDelivery Delivery { get; init; }

    /// <summary>Gets a plain sentence that says why the message failed or was held, or null otherwise. It never repeats the message text.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}
