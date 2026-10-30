namespace Autonogrammer.Presentation.Cloud;

/// <summary>How an attempt to create a cloud agent ended.</summary>
public enum CloudCreateOutcome
{
    /// <summary>The agent was created.</summary>
    Created = 0,

    /// <summary>The service refused because a cap on agents or awake workers is reached.</summary>
    LimitReached = 1,

    /// <summary>The service refused because the compute quota is used up.</summary>
    QuotaExceeded = 2,

    /// <summary>The service refused the request itself, for example an entry it does not accept.</summary>
    Rejected = 3,

    /// <summary>The attempt failed: the service could not be reached or answered with an error.</summary>
    Failed = 4,

    /// <summary>No request was made because the entries are not valid or the cost was not confirmed.</summary>
    NotReady = 5,
}
