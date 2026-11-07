namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>What happens to a browser download.</summary>
public enum BrowserUseDownloadMode
{
    /// <summary>Downloads are refused.</summary>
    Deny = 0,

    /// <summary>Downloads are held in a quarantine directory.</summary>
    Quarantine = 1,
}
