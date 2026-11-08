using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>Maps the remote kernel and machine names to the platform keys the pinned Bun has an archive for.</summary>
internal static class RemotePlatform
{
    /// <summary>Returns the Bun platform key for a remote kernel and machine, or null when no pinned Bun runs there.</summary>
    /// <param name="kernelName">The output of <c>uname -s</c>.</param>
    /// <param name="machine">The output of <c>uname -m</c>.</param>
    /// <returns>The key such as <see cref="OmpBunPin.LinuxX64"/>, or null.</returns>
    public static string? PinKeyFor(string kernelName, string machine) => (kernelName, machine) switch
    {
        ("Linux", "x86_64") => OmpBunPin.LinuxX64,
        ("Darwin", "arm64") => OmpBunPin.DarwinAarch64,
        _ => null,
    };

    /// <summary>Returns the <c>uname -s</c> and <c>uname -m</c> pair that the install scripts check before they write anything.</summary>
    /// <param name="platformKey">A platform key from <see cref="OmpBunPin"/>.</param>
    /// <returns>The kernel and machine names as <c>uname</c> prints them.</returns>
    public static (string KernelName, string Machine) UnameFor(string platformKey) => platformKey switch
    {
        OmpBunPin.LinuxX64 => ("Linux", "x86_64"),
        OmpBunPin.DarwinAarch64 => ("Darwin", "arm64"),
        _ => throw new ArgumentOutOfRangeException(nameof(platformKey), "No remote platform is known for this key."),
    };
}
