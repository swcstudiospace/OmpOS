namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>Why an acquisition finished. A silent download is not an outcome this type can produce.</summary>
public enum AcquireOutcome
{
    /// <summary>The archive was verified, extracted and published by rename.</summary>
    Installed = 0,

    /// <summary>The manifest has no row for the platform, so the install action stays hidden.</summary>
    ManagedBrowserNotPinned = 1,

    /// <summary>The user did not confirm the dialog.</summary>
    NotConfirmed = 2,

    /// <summary>The URL was not HTTPS on the allowed host, or a redirect left that host.</summary>
    RefusedHost = 3,

    /// <summary>The byte count did not match the row.</summary>
    RefusedSize = 4,

    /// <summary>The SHA-256 did not match the row.</summary>
    RefusedHash = 5,

    /// <summary>An archive entry would have written outside the target directory.</summary>
    RefusedTraversal = 6,
}
