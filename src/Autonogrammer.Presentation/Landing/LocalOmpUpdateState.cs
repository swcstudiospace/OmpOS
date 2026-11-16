namespace Autonogrammer.Presentation.Landing;

/// <summary>What the check for a newer managed omp, made after the window is shown, found.</summary>
public enum LocalOmpUpdateState
{
    /// <summary>No check has finished, or there is nothing to say about it. The landing shows no notice.</summary>
    None = 0,

    /// <summary>The active omp is the latest published version. The landing shows no notice.</summary>
    UpToDate = 1,

    /// <summary>A newer omp was installed and is now the managed install.</summary>
    Updated = 2,

    /// <summary>The registry could not be reached. The installed omp keeps running and the notice says when it was last checked.</summary>
    Offline = 3,

    /// <summary>A newer omp waits for the next launch because the omp in use cannot be replaced now.</summary>
    Deferred = 4,

    /// <summary>A newer omp failed its check after it was activated, and the previous good one was restored.</summary>
    RolledBack = 5,

    /// <summary>The check or the update failed for the reason the notice carries.</summary>
    Failed = 6,
}
