using Autonogrammer.Harness.BrowserUse.Options;

namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>
/// Finds Chromium in order: configured path, managed state directory, well-known per-OS paths. The snap binary is skipped.
/// Nothing is downloaded.
/// </summary>
public sealed class ChromiumLocator
{
    /// <summary>The snap binary that is never selected.</summary>
    public const string SnapChromiumPath = "/snap/bin/chromium";

    private readonly IChromiumProbe _probe;

    /// <summary>Initializes a locator.</summary>
    /// <param name="probe">The file-system seam.</param>
    public ChromiumLocator(IChromiumProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    /// <summary>
    /// Locates a binary. A found binary below <see cref="BrowserUseOptions.MinimumMajorVersion"/> is <see cref="BrowserLocateState.BrowserTooOld"/>
    /// and is not replaced by a later candidate.
    /// </summary>
    /// <param name="options">The browser options.</param>
    /// <param name="stateDirectory">The head state directory.</param>
    /// <param name="platform">The OS family used to choose well-known paths.</param>
    /// <returns>The locate result.</returns>
    public ChromiumLocation Locate(BrowserUseOptions options, string stateDirectory, ChromiumOsFamily platform)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(stateDirectory);
        if (!string.IsNullOrEmpty(options.ExecutablePath))
        {
            var configured = Consider(options.ExecutablePath, options.MinimumMajorVersion);
            if (configured is not null)
            {
                return configured;
            }
        }

        var managedRoot = Path.Combine(stateDirectory, "browser", "chromium");
        foreach (var versionDir in _probe.ListDirectories(managedRoot))
        {
            var managed = Consider(ManagedBinary(Path.Combine(managedRoot, versionDir), platform), options.MinimumMajorVersion);
            if (managed is not null)
            {
                return managed;
            }
        }

        foreach (var candidate in WellKnown(platform))
        {
            var system = Consider(candidate, options.MinimumMajorVersion);
            if (system is not null)
            {
                return system;
            }
        }

        return new ChromiumLocation(BrowserLocateState.BrowserNotInstalled, null, null);
    }

    private ChromiumLocation? Consider(string path, int minimumMajor)
    {
        if (IsSnap(path))
        {
            return null;
        }

        if (!_probe.Exists(path))
        {
            return null;
        }

        var major = _probe.ReadMajorVersion(path);
        if (major is null || major.Value < minimumMajor)
        {
            return new ChromiumLocation(BrowserLocateState.BrowserTooOld, path, major);
        }

        return new ChromiumLocation(BrowserLocateState.Found, path, major);
    }

    private bool IsSnap(string path)
    {
        if (string.Equals(path, SnapChromiumPath, StringComparison.Ordinal))
        {
            return true;
        }

        var target = _probe.ReadLink(path);
        return target is not null
            && (target.Contains("/snap/", StringComparison.Ordinal) || string.Equals(target, SnapChromiumPath, StringComparison.Ordinal));
    }

    private static string ManagedBinary(string versionDirectory, ChromiumOsFamily platform)
    {
        var name = platform == ChromiumOsFamily.Windows ? "chrome.exe" : "chrome";
        return Path.Combine(versionDirectory, name);
    }

    private static IReadOnlyList<string> WellKnown(ChromiumOsFamily platform)
    {
        return platform switch
        {
            ChromiumOsFamily.Windows =>
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"),
            ],
            ChromiumOsFamily.MacOs =>
            [
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                "/Applications/Chromium.app/Contents/MacOS/Chromium",
            ],
            _ =>
            [
                "/usr/bin/google-chrome",
                "/usr/bin/google-chrome-stable",
                "/usr/bin/chromium",
                "/usr/bin/chromium-browser",
                "/opt/google/chrome/chrome",
                SnapChromiumPath,
            ],
        };
    }
}
