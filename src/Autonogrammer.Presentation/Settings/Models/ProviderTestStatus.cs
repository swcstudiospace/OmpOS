namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>What a connection test found.</summary>
public enum ProviderTestStatus
{
    /// <summary>Something answered at the endpoint as expected.</summary>
    Reachable = 0,

    /// <summary>Nothing answered at the endpoint.</summary>
    NotRunning = 1,

    /// <summary>The endpoint answered but refused the credential.</summary>
    Unauthorized = 2,

    /// <summary>The test was not run: it needs credentials or access the source does not have.</summary>
    NotTestable = 3,

    /// <summary>The test ran and failed for another reason.</summary>
    Failed = 4,
}
