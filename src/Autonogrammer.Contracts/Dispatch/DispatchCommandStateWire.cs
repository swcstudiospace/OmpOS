namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Wire names for <see cref="DispatchCommandState"/>. <see cref="ToWire"/> is the only place a name is written.</summary>
public static class DispatchCommandStateWire
{
    private static readonly DispatchCommandState[] AllStates = Enum.GetValues<DispatchCommandState>();

    /// <summary>Parses one closed hub state.</summary>
    /// <param name="text">The wire value.</param>
    /// <param name="state">The state when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is one of the ten closed names.</returns>
    public static bool TryParse(string? text, out DispatchCommandState state)
    {
        foreach (var candidate in AllStates)
        {
            if (string.Equals(ToWire(candidate), text, StringComparison.Ordinal))
            {
                state = candidate;
                return true;
            }
        }

        state = default;
        return false;
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
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "The dispatch command state has no wire name."),
    };
}
