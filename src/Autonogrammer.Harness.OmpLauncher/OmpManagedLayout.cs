namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Where the managed install keeps each file. Every path is below <c>omp/</c> in the managed root, so nothing else is touched.
/// </summary>
internal sealed class OmpManagedLayout
{
    /// <summary>Creates the layout for a managed root.</summary>
    /// <param name="rootDirectory">The absolute managed root.</param>
    public OmpManagedLayout(string rootDirectory)
    {
        BaseDirectory = Path.Combine(Path.GetFullPath(rootDirectory), "omp");
    }

    /// <summary>The <c>omp</c> directory under the managed root.</summary>
    public string BaseDirectory { get; }

    /// <summary>The state record.</summary>
    public string StatePath => Path.Combine(BaseDirectory, "state.json");

    /// <summary>Where tarballs and Bun archives land before they are verified.</summary>
    public string DownloadsDirectory => Path.Combine(BaseDirectory, "downloads");

    /// <summary>Where a version is built before it is moved into <see cref="VersionsDirectory"/>.</summary>
    public string StagingDirectory => Path.Combine(BaseDirectory, "staging");

    /// <summary>Where each activated or quarantined version lives.</summary>
    public string VersionsDirectory => Path.Combine(BaseDirectory, "versions");

    /// <summary>Where the pinned Bun runtimes live.</summary>
    public string RuntimeDirectory => Path.Combine(BaseDirectory, "runtime");

    /// <summary>The directory of one installed version.</summary>
    /// <param name="version">A validated version.</param>
    /// <returns>The directory path.</returns>
    public string VersionDirectory(string version) => Path.Combine(VersionsDirectory, version);

    /// <summary>The executable of one installed version. Its name matches <see cref="OmpLocator.ExecutableName"/>.</summary>
    /// <param name="version">A validated version.</param>
    /// <returns>The executable path.</returns>
    public string ExecutablePath(string version) =>
        Path.Combine(VersionDirectory(version), "bin", OmpLocator.ExecutableName);

    /// <summary>The directory of one pinned Bun version.</summary>
    /// <param name="bunVersion">The Bun version.</param>
    /// <returns>The directory path.</returns>
    public string BunDirectory(string bunVersion) => Path.Combine(RuntimeDirectory, "bun-" + bunVersion);
}
