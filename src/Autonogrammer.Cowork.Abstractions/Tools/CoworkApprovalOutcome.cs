namespace Autonogrammer.Cowork.Abstractions;

/// <summary>How a person, or the absence of one, answered an approval request.</summary>
public enum CoworkApprovalOutcome
{
    /// <summary>Allowed this call only.</summary>
    Allowed = 0,

    /// <summary>Allowed this call and asked for a stored grant.</summary>
    AllowedAlways = 1,

    /// <summary>Refused.</summary>
    Denied = 2,

    /// <summary>No answer arrived before the timeout.</summary>
    TimedOut = 3,

    /// <summary>No desktop window could show the ask.</summary>
    Unavailable = 4,
}
