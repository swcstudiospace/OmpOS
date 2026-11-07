namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Result of a compare-and-set. Rejection is a no-op, never an error to the peer.</summary>
public enum DispatchTransitionStatus
{
    /// <summary>The row moved.</summary>
    Applied,

    /// <summary>The current state did not match, or the edge is not in the table. The row is unchanged.</summary>
    Rejected,

    /// <summary>The account does not own the command.</summary>
    NotFound,
}
