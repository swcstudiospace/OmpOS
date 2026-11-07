using Autonogrammer.Harness.BrowserUse.Options;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Deny by default. Quarantine is a 0700 directory, a 50 MiB cap and no executable extension.</summary>
public sealed class BrowserDownloadQuarantine
{
    /// <summary>The byte cap.</summary>
    public const long MaxBytes = 50L * 1024 * 1024;

    private static readonly HashSet<string> RefusedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".scr", ".msi", ".ps1", ".sh", ".app", ".dmg", ".so", ".dylib",
    };

    /// <summary>Gets the last refusal.</summary>
    public string? LastRefusal { get; private set; }

    /// <summary>Returns whether an extension is executable.</summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>True when the extension is refused.</returns>
    public static bool IsExecutable(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        return RefusedExtensions.Contains(Path.GetExtension(fileName));
    }

    /// <summary>Creates the quarantine directory at mode 0700 under the profile.</summary>
    /// <param name="profileDirectory">The session profile.</param>
    /// <returns>The directory.</returns>
    public static string EnsureDirectory(string profileDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileDirectory);
        var directory = Path.Combine(profileDirectory, "downloads");
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return directory;
    }

    /// <summary>Refuses every download in deny mode. Quarantine writes only a safe file under the cap.</summary>
    /// <param name="mode">The download mode.</param>
    /// <param name="profileDirectory">The profile.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The stored path, or null when refused.</returns>
    public string? TryStore(DownloadMode mode, string profileDirectory, string fileName, ReadOnlySpan<byte> bytes)
    {
        LastRefusal = null;
        if (mode == DownloadMode.Deny)
        {
            LastRefusal = "DownloadDenied";
            return null;
        }

        if (IsExecutable(fileName))
        {
            LastRefusal = "DownloadDenied";
            return null;
        }

        if (bytes.Length > MaxBytes)
        {
            LastRefusal = "DownloadDenied";
            return null;
        }

        var directory = EnsureDirectory(profileDirectory);
        var safeName = Path.GetFileName(fileName);
        var path = Path.Combine(directory, safeName);
        File.WriteAllBytes(path, bytes.ToArray());
        return path;
    }

    /// <summary>Copies a quarantined file into the workspace only after a MustBeLocal release.</summary>
    /// <param name="quarantinedPath">The quarantined file.</param>
    /// <param name="workspaceDirectory">The workspace.</param>
    /// <param name="released">Whether the local card allowed it.</param>
    /// <returns>The released path, or null.</returns>
    public static string? Release(string quarantinedPath, string workspaceDirectory, bool released)
    {
        if (!released || string.IsNullOrEmpty(quarantinedPath) || !File.Exists(quarantinedPath))
        {
            return null;
        }

        Directory.CreateDirectory(workspaceDirectory);
        var destination = Path.Combine(workspaceDirectory, Path.GetFileName(quarantinedPath));
        File.Copy(quarantinedPath, destination, overwrite: false);
        return destination;
    }
}
