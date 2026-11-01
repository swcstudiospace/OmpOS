namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>The OS family used to choose well-known Chromium paths. Tests pass this explicitly.</summary>
public enum ChromiumOsFamily
{
    /// <summary>Linux paths, including the skipped snap path.</summary>
    Linux = 0,

    /// <summary>macOS application-bundle paths.</summary>
    MacOs = 1,

    /// <summary>Windows Chrome paths.</summary>
    Windows = 2,
}
