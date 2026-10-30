namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// omp was not found, so no terminal was started. The rest of the application keeps working.
/// </summary>
public sealed class OmpTerminalNotFound : OmpTerminalLaunchResult
{
    /// <summary>Creates a not-found result.</summary>
    /// <param name="detectedPaths">Every path that was checked for omp, in search order, without duplicates.</param>
    /// <param name="version">
    /// The installed omp version. It is null whenever omp was not found, because no executable exists to report one; the
    /// member is part of the result so a panel can show path, version and guidance in one place.
    /// </param>
    /// <param name="installGuidance">Text that tells the operator how to make omp available.</param>
    /// <exception cref="ArgumentNullException"><paramref name="detectedPaths"/> or <paramref name="installGuidance"/> is null.</exception>
    public OmpTerminalNotFound(IReadOnlyList<string> detectedPaths, string? version, string installGuidance)
    {
        ArgumentNullException.ThrowIfNull(detectedPaths);
        ArgumentNullException.ThrowIfNull(installGuidance);
        DetectedPaths = detectedPaths;
        Version = version;
        InstallGuidance = installGuidance;
    }

    /// <summary>Every path that was checked for omp, in search order, without duplicates.</summary>
    public IReadOnlyList<string> DetectedPaths { get; }

    /// <summary>The installed omp version. It is null whenever omp was not found.</summary>
    public string? Version { get; }

    /// <summary>Text that tells the operator how to make omp available.</summary>
    public string InstallGuidance { get; }
}
