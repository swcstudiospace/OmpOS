namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>The result of looking for a Chromium binary. Acquisition is a separate, user-confirmed step.</summary>
public enum BrowserLocateState
{
    /// <summary>A binary at or above the minimum major version was found.</summary>
    Found = 0,

    /// <summary>No configured, managed or system binary was found.</summary>
    BrowserNotInstalled = 1,

    /// <summary>A binary was found and its major version is below the minimum.</summary>
    BrowserTooOld = 2,
}
