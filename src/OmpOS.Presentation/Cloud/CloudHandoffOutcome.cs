namespace OmpOS.Presentation.Cloud;

/// <summary>How a hand-off of a prompt to a cloud agent ended.</summary>
public enum CloudHandoffOutcome
{
    /// <summary>A cloud agent was created and the prompt is its first task.</summary>
    Started = 0,

    /// <summary>No request was made: the prompt or a repository is missing or not valid.</summary>
    NeedsInput = 1,

    /// <summary>No request was made: the caller did not set <see cref="CloudHandoffRequest.CostConfirmed"/>.</summary>
    NeedsCostConfirmation = 2,

    /// <summary>Cloud agents are not set up, or the user is not signed in.</summary>
    NotAvailable = 3,

    /// <summary>The service refused because a cap is reached.</summary>
    LimitReached = 4,

    /// <summary>The service refused because the compute quota is used up.</summary>
    QuotaExceeded = 5,

    /// <summary>The service refused the request itself.</summary>
    Rejected = 6,

    /// <summary>The hand-off failed.</summary>
    Failed = 7,
}
