using System.Runtime.InteropServices;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The Bun runtime that runs <c>bun install</c> and <c>bun build --compile</c> for a managed omp install. The pin names one Bun
/// version and one archive per platform. Each SHA-256 was read from that release's <c>SHASUMS256.txt</c> and checked against
/// the downloaded archive.
/// </summary>
/// <param name="Version">The pinned Bun version, such as <c>1.4.4</c>.</param>
/// <param name="Assets">The archive for each platform key. The keys are the platform constants on this type.</param>
public sealed record OmpBunPin(string Version, IReadOnlyDictionary<string, OmpBunAsset> Assets)
{
    /// <summary>The platform key for Linux on x64.</summary>
    public const string LinuxX64 = "linux-x64";

    /// <summary>The platform key for Windows on x64.</summary>
    public const string WindowsX64 = "windows-x64";

    /// <summary>The platform key for macOS on Apple silicon.</summary>
    public const string DarwinAarch64 = "darwin-aarch64";

    /// <summary>
    /// The pin shipped with this release. Bun 1.4.4 is the first pinned release that satisfies omp 18.18.1, which requires
    /// <c>bun &gt;=1.4.4</c>.
    /// </summary>
    public static OmpBunPin Default { get; } = new(
        "1.4.4",
        new Dictionary<string, OmpBunAsset>(StringComparer.Ordinal)
        {
            [LinuxX64] = new("bun-linux-x64.zip", "bf8ccbfb2b0b80159e0f9a6d15c55317add8077534dbc6130a3edb52fccb395c"),
            [WindowsX64] = new("bun-windows-x64.zip", "cd2c0fccc78f3ede1f50ca827a15f063eda13d4d150a3d0674f4f6e323d864a5"),
            [DarwinAarch64] = new("bun-darwin-aarch64.zip", "c240d7f0ccb90c263a849fcdb09b7ee62630d921348ed6aafda86b9d577bc510"),
        });

    /// <summary>Returns the platform key for this process, or null when no Bun is pinned for it.</summary>
    /// <returns>The key, such as <c>linux-x64</c>, or null.</returns>
    public static string? CurrentPlatformKey()
    {
        var architecture = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsLinux() && architecture == Architecture.X64)
        {
            return LinuxX64;
        }

        if (OperatingSystem.IsWindows() && architecture == Architecture.X64)
        {
            return WindowsX64;
        }

        if (OperatingSystem.IsMacOS() && architecture == Architecture.Arm64)
        {
            return DarwinAarch64;
        }

        return null;
    }

    /// <summary>The Bun executable's file name on <paramref name="platformKey"/>.</summary>
    /// <param name="platformKey">A platform key from this type.</param>
    /// <returns><c>bun.exe</c> on Windows, otherwise <c>bun</c>.</returns>
    public static string ExecutableName(string platformKey)
    {
        ArgumentNullException.ThrowIfNull(platformKey);
        return platformKey.StartsWith("windows-", StringComparison.Ordinal) ? "bun.exe" : "bun";
    }

    /// <summary>Returns the archive pinned for <paramref name="platformKey"/>.</summary>
    /// <param name="platformKey">The platform key, or null when it is not known.</param>
    /// <returns>The archive, or null when none is pinned for the platform.</returns>
    public OmpBunAsset? AssetFor(string? platformKey) =>
        platformKey is not null && Assets.TryGetValue(platformKey, out var asset) ? asset : null;
}
