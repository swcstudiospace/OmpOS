namespace Autonogrammer.Harness.BrowserUse.Options;

/// <summary>What happens to a browser download. Only deny and quarantine are valid.</summary>
public enum DownloadMode
{
    /// <summary>Downloads are refused.</summary>
    Deny = 0,

    /// <summary>Downloads land in a quarantined directory until a local card releases them.</summary>
    Quarantine = 1,
}
