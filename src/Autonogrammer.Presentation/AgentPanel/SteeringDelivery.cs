namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// What a live steering channel reported for one message: the delivery status for the history entry and, for a failure, the plain
/// reason the status line and the entry show. Presentation never references the agent-loop library, so the head's channel adapter
/// translates its receipts, acknowledgements and exceptions into this value.
/// </summary>
/// <param name="Status">The delivery status the channel can prove.</param>
/// <param name="Reason">The plain reason of a <see cref="SteeringDeliveryStatus.Failed"/> delivery, or null for any other status.</param>
public sealed record SteeringDelivery(SteeringDeliveryStatus Status, string? Reason = null)
{
    /// <summary>Gets whether the message did not reach the session, so the draft should stay where the user typed it.</summary>
    public bool IsFailure => Status == SteeringDeliveryStatus.Failed;

    /// <summary>Creates the delivery of a message the channel could not deliver.</summary>
    /// <param name="reason">The plain reason, safe to show.</param>
    /// <returns>A failed delivery.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    public static SteeringDelivery Failed(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return new SteeringDelivery(SteeringDeliveryStatus.Failed, reason);
    }
}
