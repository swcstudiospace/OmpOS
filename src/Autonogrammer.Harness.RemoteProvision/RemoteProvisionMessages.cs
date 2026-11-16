using System.Globalization;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>The sentences the provisioning results carry: the action lines the user confirms and the reasons for drift and refusals.</summary>
internal static class RemoteProvisionMessages
{
    /// <summary>The number of characters of a commit shown in a sentence.</summary>
    public const int ShortCommitLength = 12;

    /// <summary>The reason a failed action's dependents are not started.</summary>
    public const string NotRunReason = "Not run, because an earlier install failed.";

    /// <summary>The message for an install that did not finish in time.</summary>
    public const string InstallTimedOut = "The install did not finish in time.";

    /// <summary>The message for a probe that exited with an error code.</summary>
    /// <param name="exitCode">The exit code the probe reported.</param>
    /// <returns>The sentence.</returns>
    public static string ProbeExited(int exitCode) => "The probe exited with code " + exitCode.ToString(CultureInfo.InvariantCulture) + ".";

    /// <summary>The message for a probe answer that could not be read.</summary>
    /// <param name="detail">What was wrong with the answer.</param>
    /// <returns>The sentence.</returns>
    public static string ProbeUnreadable(string detail) => "The probe answer could not be read: " + detail;

    /// <summary>The message for an install script that exited with an error code.</summary>
    /// <param name="exitCode">The exit code the script reported.</param>
    /// <returns>The sentence.</returns>
    public static string InstallExited(int exitCode) => "The install exited with code " + exitCode.ToString(CultureInfo.InvariantCulture) + ".";

    /// <summary>The install line for Bun.</summary>
    public static string BunAction(string version, string path) => "Install Bun " + version + " into " + path;

    /// <summary>The install line for omp.</summary>
    public static string OmpAction(string version, string path, string bunVersion) =>
        "Install Omp " + version + " into " + path + " (built with Bun " + bunVersion + ")";

    /// <summary>The install line for one plugin.</summary>
    public static string PluginAction(string dir, string commit, string source, string path) =>
        "Install plugin " + dir + " at " + Short(commit) + " from " + source + " into " + path;

    /// <summary>Why Bun differs from the pin.</summary>
    public static string BunDrift(string? current, string pinned) => current is null
        ? "Bun is not installed; the pinned version is " + pinned + "."
        : "Bun " + current + " is installed; the pinned version is " + pinned + ".";

    /// <summary>Why omp differs from the latest release.</summary>
    public static string OmpDrift(string? current, string latest) => current is null
        ? "Omp is not installed; the latest release is " + latest + "."
        : "Omp " + current + " is installed; the latest release is " + latest + ".";

    /// <summary>Why a plugin differs from its bundle entry.</summary>
    public static string PluginDrift(string? remoteCommit, string bundleCommit) => remoteCommit is null
        ? "The plugin is not installed; the bundle pins " + Short(bundleCommit) + "."
        : "The plugin is at " + Short(remoteCommit) + "; the bundle pins " + Short(bundleCommit) + ".";

    /// <summary>Why an install cannot run: the tools it needs that are not on the remote path.</summary>
    public static string MissingTools(string need, IReadOnlyList<string> missing) =>
        "Needs " + need + "; not on the remote path: " + string.Join(", ", missing) + ".";

    /// <summary>Why Omp cannot be installed while the pinned Bun cannot be.</summary>
    public static string NeedsBun(string version) => "Needs Bun " + version + ", which cannot be installed here.";

    /// <summary>Why an in-repo plugin is not installed.</summary>
    public static string InRepoPlugin(string dir) =>
        "The plugin " + dir + " ships inside the desktop checkout, and the remote machine cannot fetch it.";

    /// <summary>Why a plugin with dependencies is not installed.</summary>
    public static string PluginNeedsInstall(string dir) =>
        "The plugin " + dir + " declares runtime dependencies, and the remote install does not run dependency installs.";

    /// <summary>Why the desired omp release cannot be read.</summary>
    public static string RegistryUnavailable(string detail) => "The omp release could not be read: " + detail;

    /// <summary>Why the registry read timed out.</summary>
    public static string RegistryTimedOut => "The omp release read timed out.";

    /// <summary>Why the release's engines range is one this build cannot check.</summary>
    public static string EnginesUnsupported(string range, string pinned) =>
        "The release requires Bun " + range + "; this build can check only a minimum version, and pins Bun " + pinned + ".";

    /// <summary>Why the release's engines range is not met by the pinned Bun.</summary>
    public static string EnginesNotMet(string range, string pinned) =>
        "The release requires Bun " + range + ", and the pinned Bun is " + pinned + ".";

    /// <summary>The message for a pinned Bun that has no archive for a platform key.</summary>
    public static string NoBunArchive(string platformKey) => "No Bun archive is pinned for " + platformKey + ".";

    /// <summary>The path of the pinned Bun under the user's home.</summary>
    public static string BunPath(string home) => home + "/.bun/bin/bun";

    /// <summary>The path of the omp link under the user's home.</summary>
    public static string OmpPath(string home) => home + "/.local/bin/omp";

    /// <summary>The directory a plugin is installed into under the user's home.</summary>
    public static string PluginDirectory(string home, string dir) => home + "/.local/share/autonogrammer/plugins/" + dir;

    /// <summary>Returns the short form of a commit for a sentence.</summary>
    public static string Short(string commit) => commit.Length <= ShortCommitLength ? commit : commit[..ShortCommitLength];
}
