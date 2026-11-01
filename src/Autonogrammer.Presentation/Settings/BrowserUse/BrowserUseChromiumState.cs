namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>What the Settings page says about the Chromium binary. Acquisition is a separate step.</summary>
public enum BrowserUseChromiumState
{
    /// <summary>A binary at or above the minimum major version was found.</summary>
    Ready = 0,

    /// <summary>No configured, managed or system binary was found, and a manifest row exists.</summary>
    BrowserNotInstalled = 1,

    /// <summary>A binary was found and its major version is below the minimum.</summary>
    BrowserTooOld = 2,

    /// <summary>No reviewed archive is pinned for this platform, so the install action stays hidden.</summary>
    ManagedBrowserNotPinned = 3,
}
