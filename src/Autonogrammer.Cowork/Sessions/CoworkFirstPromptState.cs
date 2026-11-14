namespace Autonogrammer.Cowork.Sessions;

/// <summary>Where the standing block of one Cowork task stands in its delivery.</summary>
public enum CoworkFirstPromptState
{
    /// <summary>Stored and not yet written to the transport. The next send carries the block, once.</summary>
    Pending,

    /// <summary>The send is starting or running in this run. A run that opens a record an earlier run left in this state treats it as unknown.</summary>
    InFlight,

    /// <summary>The block was written but its outcome is unknown: the response was lost, or the send was cancelled after it left. Sends wait for the user.</summary>
    Unconfirmed,

    /// <summary>omp answered the send, or replayed the block when the session was loaded. The block is not sent again.</summary>
    Acknowledged,

    /// <summary>The user chose to start without the block. The block is not sent.</summary>
    Abandoned,
}
