namespace Autonogrammer.AgentWorker.Bridge;

/// <summary>The result of an account purge.</summary>
internal enum AccountPurgeOutcome
{
    /// <summary>The account directory was removed.</summary>
    Deleted,

    /// <summary>The account directory does not exist; nothing was deleted.</summary>
    NotFound,

    /// <summary>A session of the account is attached; nothing was deleted.</summary>
    Attached,
}
