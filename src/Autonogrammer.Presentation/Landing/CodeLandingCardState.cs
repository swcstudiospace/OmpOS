namespace Autonogrammer.Presentation.Landing;

/// <summary>The state a card is in. Each state has one message and at most one next action.</summary>
public enum CodeLandingCardState
{
    /// <summary>The status of the environment is still being read.</summary>
    Checking = 0,

    /// <summary>Reading the status threw, or an install did not complete. The card offers a retry.</summary>
    Failed = 1,

    /// <summary>The environment is ready to start an agent on.</summary>
    Ready = 2,

    /// <summary>The environment has nothing to choose from yet.</summary>
    Empty = 3,

    /// <summary>The account is signed out. The card offers sign-in.</summary>
    SignedOut = 4,

    /// <summary>The account is signed in but the service cannot be reached.</summary>
    Offline = 5,

    /// <summary>The API has no sign-in configured, so the card cannot be used.</summary>
    NotConfigured = 6,

    /// <summary>The environment cannot be used here (omp is missing, or the source is not wired in this window).</summary>
    Unavailable = 7,

    /// <summary>A managed install of omp is running. The card shows its progress and offers Cancel.</summary>
    Installing = 8,
}
