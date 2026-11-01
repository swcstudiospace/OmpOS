namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Wire names for <see cref="DispatchCommandState"/>.</summary>
public static class DispatchCommandStateWire
{
    /// <summary>Parses one closed hub state.</summary>
    /// <param name="text">The wire value.</param>
    /// <param name="state">The state when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is one of the ten closed names.</returns>
    public static bool TryParse(string? text, out DispatchCommandState state)
    {
        switch (text)
        {
            case "queued":
                state = DispatchCommandState.Queued;
                return true;
            case "delivered":
                state = DispatchCommandState.Delivered;
                return true;
            case "received":
                state = DispatchCommandState.Received;
                return true;
            case "awaiting_approval":
                state = DispatchCommandState.AwaitingApproval;
                return true;
            case "accepted":
                state = DispatchCommandState.Accepted;
                return true;
            case "running":
                state = DispatchCommandState.Running;
                return true;
            case "completed":
                state = DispatchCommandState.Completed;
                return true;
            case "failed":
                state = DispatchCommandState.Failed;
                return true;
            case "cancelled":
                state = DispatchCommandState.Cancelled;
                return true;
            case "expired":
                state = DispatchCommandState.Expired;
                return true;
            default:
                state = default;
                return false;
        }
    }

    /// <summary>Returns the wire name of a hub state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The exact protocol string.</returns>
    public static string ToWire(DispatchCommandState state) => state switch
    {
        DispatchCommandState.Queued => "queued",
        DispatchCommandState.Delivered => "delivered",
        DispatchCommandState.Received => "received",
        DispatchCommandState.AwaitingApproval => "awaiting_approval",
        DispatchCommandState.Accepted => "accepted",
        DispatchCommandState.Running => "running",
        DispatchCommandState.Completed => "completed",
        DispatchCommandState.Failed => "failed",
        DispatchCommandState.Cancelled => "cancelled",
        DispatchCommandState.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
