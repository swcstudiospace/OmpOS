namespace Autonogrammer.Api.Operations;

/// <summary>The outcome of a purge a worker accepted.</summary>
internal enum WorkerPurgeOutcome
{
    /// <summary>The worker deleted the account's workspace folder.</summary>
    Purged,

    /// <summary>The worker had no folder for the account.</summary>
    NotFound,
}
