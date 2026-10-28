namespace Autonogrammer.Presentation.Cloud;

/// <summary>Why a cloud host call failed, in the terms the presentation reacts to differently.</summary>
public enum CloudFailureKind
{
    /// <summary>The service cannot be reached or answered with an error. Trying again may work.</summary>
    Unavailable = 0,

    /// <summary>Cloud agents are not configured on this installation.</summary>
    NotConfigured = 1,

    /// <summary>The user is not signed in to the service or may not use it.</summary>
    Unauthorized = 2,

    /// <summary>A cap on agents or awake workers was reached, so nothing more can be started.</summary>
    LimitReached = 3,

    /// <summary>The compute quota is used up.</summary>
    QuotaExceeded = 4,

    /// <summary>The agent does not exist, or it belongs to someone else.</summary>
    NotFound = 5,

    /// <summary>The agent is in a state that does not allow the request, for example waking a running agent.</summary>
    Conflict = 6,

    /// <summary>The service refused the request because an entry is not acceptable.</summary>
    Rejected = 7,
}
