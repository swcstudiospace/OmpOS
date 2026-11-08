using System.Globalization;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The user-readable text for every managed omp outcome. Each outcome's text is defined here and nowhere else.
/// </summary>
public static class OmpManagedMessages
{
    /// <summary>A TLS handshake or certificate chain was rejected.</summary>
    public const string TlsTrust =
        "The connection to the omp registry failed certificate validation. A TLS-intercepting proxy or a missing root certificate is the usual cause. " +
        "Check the system trust store and the proxy settings. Certificate validation is never disabled.";

    /// <summary>There is no broken managed install to reinstall or roll back.</summary>
    public const string NothingToRecover = "No broken managed omp install needs recovery.";

    /// <summary>No earlier version is recorded to roll back to.</summary>
    public const string NoPreviousGood = "There is no previous good omp version to roll back to.";

    /// <summary>Returns the text for a permission failure in <paramref name="directory"/>.</summary>
    /// <param name="directory">The managed directory the user must be able to write.</param>
    /// <returns>The message.</returns>
    public static string PermissionDenied(string directory) =>
        "omp could not read or write its managed directory " + directory + ". Check that the current user owns it. " +
        "Autonogrammer does not request elevation.";

    /// <summary>Returns the text for a file blocked right after a write or a run.</summary>
    /// <param name="directory">The managed directory to allowlist in antivirus software.</param>
    /// <returns>The message.</returns>
    public static string AntivirusBlocked(string directory) =>
        "A file in the managed omp directory " + directory + " was blocked right after it was written or run. Antivirus software may have " +
        "quarantined it. Allowlist that directory and try again.";

    /// <summary>Returns the text for a move that a permission error deferred.</summary>
    /// <param name="directory">The managed directory the user must be able to write.</param>
    /// <returns>The message.</returns>
    public static string DeferredPermission(string directory) =>
        PermissionDenied(directory) + " The staged build is kept and the move is retried on the next launch.";

    /// <summary>Returns the text for a move that a locked file deferred.</summary>
    /// <param name="version">The staged version.</param>
    /// <returns>The message.</returns>
    public static string Deferred(string version) =>
        "omp " + version + " is staged and verified, but a file is in use, so it could not be moved into place. A running omp may hold " +
        "the previous version. The staged build is kept and the move is retried on the next launch.";

    /// <summary>Returns the text for an update held back because the active executable is locked.</summary>
    /// <param name="version">The active version.</param>
    /// <returns>The message.</returns>
    public static string ActiveLocked(string version) =>
        "The active omp " + version + " is in use, so the update waits until it is released. The active install is unchanged.";

    /// <summary>Returns the stale notice shown when the registry cannot be reached.</summary>
    /// <param name="version">The active version, or null when no managed install exists.</param>
    /// <param name="checkedAt">When the registry was last checked successfully.</param>
    /// <returns>The message.</returns>
    public static string Offline(string? version, DateTimeOffset? checkedAt) =>
        version is null
            ? "The omp registry could not be reached, and no managed omp is installed yet. The next launch retries."
            : "Offline. Showing omp " + version + ", last checked " + Stamp(checkedAt) + ".";

    /// <summary>Returns the text for a download that ended before its declared length.</summary>
    /// <param name="artifact">The name of the download, such as <c>omp 2.0.0</c>.</param>
    /// <returns>The message.</returns>
    public static string PartialDownload(string artifact) =>
        "The download of " + artifact + " ended before its declared length. The partial file was deleted; the next launch retries.";

    /// <summary>Returns the text for a download that failed its digest.</summary>
    /// <param name="artifact">The name of the download.</param>
    /// <returns>The message.</returns>
    public static string IntegrityFailed(string artifact) =>
        "The download of " + artifact + " did not match its digest. The file was deleted and nothing was installed.";

    /// <summary>Returns the text for a download that the server refused.</summary>
    /// <param name="artifact">The name of the download.</param>
    /// <returns>The message.</returns>
    public static string HttpStatus(string artifact) =>
        "The download of " + artifact + " was refused by the server. Nothing was installed.";

    /// <summary>Returns the text for an omp release that needs a newer Bun than the pin.</summary>
    /// <param name="version">The omp version.</param>
    /// <param name="range">The engines.bun range the release declares.</param>
    /// <param name="pin">The pinned Bun version.</param>
    /// <returns>The message.</returns>
    public static string IncompatibleVersion(string version, string range, string pin) =>
        "omp " + version + " requires Bun " + range + ", but the pinned Bun is " + pin + ". Nothing was installed. " +
        "Raise the Bun pin in OmpBunPin to satisfy the range.";

    /// <summary>Returns the text for an engines.bun range that cannot be read.</summary>
    /// <param name="version">The omp version.</param>
    /// <param name="range">The declared range.</param>
    /// <returns>The message.</returns>
    public static string UnreadableEngines(string version, string range) =>
        "omp " + version + " declares a Bun range that cannot be read: " + range + ". Nothing was installed.";

    /// <summary>Returns the text for a platform with no pinned Bun.</summary>
    /// <param name="platform">The platform key, or null when it is not recognised.</param>
    /// <returns>The message.</returns>
    public static string UnsupportedPlatform(string? platform) =>
        "There is no pinned Bun for this platform (" + (platform ?? "unknown") + "), so omp cannot be installed here.";

    /// <summary>Returns the text for a Bun archive that has no Bun executable.</summary>
    /// <param name="bunVersion">The pinned Bun version.</param>
    /// <returns>The message.</returns>
    public static string BunMissingExecutable(string bunVersion) =>
        "The Bun " + bunVersion + " archive did not contain the Bun executable.";

    /// <summary>Returns the text for an archive entry that was refused.</summary>
    /// <param name="reason">Which entry was refused, and why.</param>
    /// <returns>The message.</returns>
    public static string UnsafeArchive(string reason) =>
        "The omp package archive contains an entry that is not allowed (" + reason + "). Nothing was extracted.";

    /// <summary>Returns the text for an archive that is corrupt or has the wrong shape.</summary>
    /// <param name="reason">What is wrong with the archive.</param>
    /// <returns>The message.</returns>
    public static string InvalidArchive(string reason) =>
        "The omp package archive is not usable: " + reason + ".";

    /// <summary>Returns the text for a build step that exited with an error.</summary>
    /// <param name="step">The step that failed.</param>
    /// <param name="output">The tail of the step's output.</param>
    /// <returns>The message.</returns>
    public static string ToolFailed(string step, string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return "Building omp failed at the " + step + " step. " + (output.Length == 0 ? "The step printed nothing." : output);
    }

    /// <summary>Returns the text for a build step that timed out.</summary>
    /// <param name="step">The step that timed out.</param>
    /// <returns>The message.</returns>
    public static string ToolTimedOut(string step) =>
        "Building omp timed out at the " + step + " step.";

    /// <summary>Returns the text for a health check that failed.</summary>
    /// <param name="version">The version under test.</param>
    /// <param name="output">The tail of the probe's output.</param>
    /// <returns>The message.</returns>
    public static string HealthCheckFailed(string version, string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return "The omp " + version + " executable did not pass its health check. " +
            (output.Length == 0 ? "It printed nothing." : "Its output was: " + output);
    }

    /// <summary>Returns the text for a health check that timed out.</summary>
    /// <param name="version">The version under test.</param>
    /// <returns>The message.</returns>
    public static string HealthCheckTimedOut(string version) =>
        "The omp " + version + " executable did not answer its health check in time.";

    /// <summary>Returns the text for an activated version that failed its probe and was rolled back.</summary>
    /// <param name="failed">The version that failed.</param>
    /// <param name="restored">The version restored in its place.</param>
    /// <returns>The message.</returns>
    public static string RolledBack(string failed, string restored) =>
        "omp " + failed + " failed its health check after activation, so omp " + restored + " was restored. " + failed +
        " is quarantined until a newer release is published.";

    /// <summary>Returns the text for a failed activation with no earlier version to restore.</summary>
    /// <param name="version">The version that failed.</param>
    /// <returns>The message.</returns>
    public static string NoPreviousVersion(string version) =>
        "omp " + version + " failed its health check after activation, and no earlier version exists to restore. " + version +
        " is quarantined.";

    /// <summary>Returns the text for an install of a quarantined version.</summary>
    /// <param name="version">The quarantined version.</param>
    /// <returns>The message.</returns>
    public static string Quarantined(string version) =>
        "omp " + version + " failed its health check earlier and is quarantined. The install waits for a newer release.";

    /// <summary>Returns the text for a reinstall of a version that is already active and intact.</summary>
    /// <param name="version">The active version.</param>
    /// <returns>The message.</returns>
    public static string AlreadyActive(string version) =>
        "omp " + version + " is already the active version and is intact, so there is nothing to install.";

    /// <summary>Returns the text for an active executable that does not match its recorded digest.</summary>
    /// <param name="version">The active version.</param>
    /// <param name="rollbackAvailable">Whether a previous good version can be restored.</param>
    /// <returns>The message.</returns>
    public static string Broken(string version, bool rollbackAvailable) =>
        "The managed omp " + version + " does not match its recorded SHA-256, so it was not run. " +
        (rollbackAvailable ? "Reinstall it, or roll back to the previous good version." : "Reinstall it.");

    /// <summary>Returns the text for a state file that cannot be read.</summary>
    /// <param name="path">The state file.</param>
    /// <returns>The message.</returns>
    public static string StateUnreadable(string path) =>
        "The managed omp state file cannot be read: " + path + ". Delete it to reset the managed install.";

    /// <summary>Returns the text for a registry response that is not usable.</summary>
    /// <param name="reason">What is wrong with the response.</param>
    /// <returns>The message.</returns>
    public static string InvalidRegistryResponse(string reason) =>
        "The omp registry returned an unexpected response: " + reason + ".";

    /// <summary>Returns the text for a version that is not a plain semantic version.</summary>
    /// <param name="version">The version as read.</param>
    /// <returns>The message.</returns>
    public static string InvalidVersion(string version) =>
        "The version " + version + " is not a plain semantic version, so it was not installed.";

    /// <summary>Returns the text for an installed version that is current.</summary>
    /// <param name="version">The active version.</param>
    /// <returns>The message.</returns>
    public static string UpToDate(string version) => "omp " + version + " is the latest published version.";

    /// <summary>Returns the text for a successful activation.</summary>
    /// <param name="version">The new active version.</param>
    /// <param name="previous">The version it replaced, or null for a first install.</param>
    /// <returns>The message.</returns>
    public static string Updated(string version, string? previous) =>
        "omp " + version + " is installed and active" + (previous is null ? "." : ", replacing " + previous + ".");

    /// <summary>Returns the text for a rollback away from a broken active version.</summary>
    /// <param name="broken">The broken version.</param>
    /// <param name="restored">The version restored.</param>
    /// <returns>The message.</returns>
    public static string RolledBackFromBroken(string broken, string restored) =>
        "omp " + restored + " is active again, in place of the broken " + broken + ".";

    private static string Stamp(DateTimeOffset? checkedAt) =>
        checkedAt is { } time ? time.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : "never";
}
