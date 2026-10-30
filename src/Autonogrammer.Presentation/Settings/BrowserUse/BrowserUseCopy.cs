namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>Every text of the Browser settings page and its acquisition dialog.</summary>
public static class BrowserUseCopy
{
    /// <summary>Navigation label.</summary>
    public const string Title = "Browser";

    /// <summary>Page heading.</summary>
    public const string Heading = "Browser";

    /// <summary>What the page is for.</summary>
    public const string Description = "Find, acquire and govern the Cowork Chromium. Nothing is downloaded until you confirm.";

    /// <summary>Action shown when a binary is ready.</summary>
    public const string ReadyAction = "Check again";

    /// <summary>No binary was found.</summary>
    public const string BrowserNotInstalledSentence = "No Chromium binary was found.";

    /// <summary>Action shown when no binary was found.</summary>
    public const string BrowserNotInstalledAction = "Install a browser";

    /// <summary>The found binary is below the minimum major version.</summary>
    public const string BrowserTooOldSentence = "The found Chromium is older than the minimum major version.";

    /// <summary>Action shown when the binary is too old.</summary>
    public const string BrowserTooOldAction = "Install a newer browser";

    /// <summary>No reviewed archive is pinned for this platform.</summary>
    public const string ManagedBrowserNotPinnedSentence = "No managed Chromium is pinned for this platform.";

    /// <summary>Action shown when no archive is pinned.</summary>
    public const string ManagedBrowserNotPinnedAction = "Set the executable path";

    /// <summary>Risk line for <see cref="BrowserUsePolicyMode.Open"/>.</summary>
    public const string OpenRisk = "Open skips the new-origin card. A dispatch turn still asks.";

    /// <summary>Risk line for trusted local origins.</summary>
    public const string LocalOriginsRisk = "Granted local origins are treated as trusted.";

    /// <summary>Licence line named by the acquisition dialog.</summary>
    public const string LicenceLine = "Chromium is BSD-3-Clause and bundles other licences. It is not shipped with omp.";

    /// <summary>Label of the executable path field.</summary>
    public const string ExecutablePathLabel = "Executable path";

    /// <summary>Label of the policy picker.</summary>
    public const string PolicyModeLabel = "Origin policy";

    /// <summary>Label of the allowed-host editor.</summary>
    public const string AllowedHostsLabel = "Allowed hosts";

    /// <summary>Label of the denied-host editor.</summary>
    public const string DeniedHostsLabel = "Denied hosts";

    /// <summary>Label of the download picker.</summary>
    public const string DownloadModeLabel = "Downloads";

    /// <summary>Label of the sandbox picker.</summary>
    public const string SandboxModeLabel = "Sandbox";

    /// <summary>Label of the persistent profile switch.</summary>
    public const string PersistentProfileLabel = "Keep a named profile";

    /// <summary>Label of the dispatch switch.</summary>
    public const string AllowDispatchSessionsLabel = "Allow dispatch sessions";

    /// <summary>Label of the local-origins switch.</summary>
    public const string LocalOriginsTrustedLabel = "Trust granted local origins";

    /// <summary>Label of the install action, shown only when a manifest row exists.</summary>
    public const string AcquireLabel = "Install a browser";

    /// <summary>Confirm button of the acquisition dialog.</summary>
    public const string ConfirmLabel = "Download";

    /// <summary>Dismiss button of the acquisition dialog.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>Accessible name of the acquisition dialog.</summary>
    public const string DialogName = "Confirm Chromium download";

    /// <summary>The ready-state sentence for a major version.</summary>
    /// <param name="version">The major version.</param>
    /// <returns>The sentence.</returns>
    public static string ReadySentence(string version) => "Chromium " + version + " is ready.";
}
