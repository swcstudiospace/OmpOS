namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The omp executable a launch runs, and the source that supplied it. The order is: an explicitly configured absolute path that
/// is runnable; then the managed active install, only when its SHA-256 matches the state record and its version probe passes;
/// then the first runnable omp on PATH and in the known install locations.
/// </summary>
/// <param name="Source">Which source won.</param>
/// <param name="ExecutablePath">The executable, or null when none was found.</param>
/// <param name="Reason">Why this source won, or why nothing was found.</param>
public sealed record OmpResolution(OmpResolutionSource Source, string? ExecutablePath, string Reason)
{
    /// <summary>
    /// Resolves the omp executable for a launch. The managed install is run only after its active executable hashes to the
    /// recorded SHA-256, so a modified file is never executed.
    /// </summary>
    /// <param name="managed">The managed options. Their root locates the state record.</param>
    /// <param name="configuredPath">An absolute path from configuration, or null when none is configured.</param>
    /// <param name="pathOptions">The PATH directories and known install locations. Its own configured path is ignored.</param>
    /// <param name="cancellationToken">Cancels the hash and the version probe.</param>
    /// <returns>The resolution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="managed"/> or <paramref name="pathOptions"/> is null.</exception>
    /// <exception cref="ArgumentException">The configured path is not absolute, or a managed option is malformed.</exception>
    public static async Task<OmpResolution> ResolveAsync(
        OmpManagedOptions managed,
        string? configuredPath,
        OmpLocatorOptions pathOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(managed);
        ArgumentNullException.ThrowIfNull(pathOptions);
        managed.Validate();
        var notes = new List<string>();
        if (configuredPath is not null)
        {
            if (OmpLocator.Locate(new OmpLocatorOptions(configuredPath, [], [])) is OmpFound configured)
            {
                return new OmpResolution(OmpResolutionSource.Configured, configured.ExecutablePath, "The configured omp path is runnable.");
            }

            notes.Add("The configured omp path is not runnable: " + configuredPath + ".");
        }

        var managedResolution = await ResolveManagedAsync(managed, notes, cancellationToken).ConfigureAwait(false);
        if (managedResolution is not null)
        {
            return managedResolution;
        }

        if (OmpLocator.Locate(new OmpLocatorOptions(null, pathOptions.SearchDirectories, pathOptions.KnownInstallLocations)) is OmpFound onPath)
        {
            notes.Add("Using the omp found on PATH or in a known install location.");
            return new OmpResolution(OmpResolutionSource.Path, onPath.ExecutablePath, string.Join(' ', notes));
        }

        notes.Add(OmpLocator.InstallGuidance);
        return new OmpResolution(OmpResolutionSource.None, null, string.Join(' ', notes));
    }

    private static async Task<OmpResolution?> ResolveManagedAsync(OmpManagedOptions managed, List<string> notes, CancellationToken cancellationToken)
    {
        var layout = new OmpManagedLayout(managed.RootDirectory);
        var store = new OmpManagedStateStore(layout.StatePath);
        OmpManagedState state;
        try
        {
            state = store.Load();
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            notes.Add("The managed omp state file cannot be read, so the managed install was skipped.");
            return null;
        }

        if (state.ActiveVersion is not { } version)
        {
            notes.Add("No managed omp install is active.");
            return null;
        }

        var executable = layout.ExecutablePath(version);
        if (!File.Exists(executable))
        {
            notes.Add("The managed omp " + version + " is missing its executable.");
            return null;
        }

        var actual = await OmpHashing.Sha256Async(executable, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actual, state.ActiveSha256, StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("The managed omp " + version + " does not match its recorded SHA-256, so it was not run.");
            return null;
        }

        var workingDirectory = Path.GetDirectoryName(executable) ?? layout.BaseDirectory;
        var probe = await managed.ProcessRunner.RunAsync(
            new OmpProcessRequest(executable, OmpCommands.VersionArguments, workingDirectory, managed.HealthCheckTimeout),
            cancellationToken).ConfigureAwait(false);
        if (probe.Outcome == OmpProcessOutcome.Exited && probe.ExitCode == 0 && probe.Output.Contains(version, StringComparison.Ordinal))
        {
            return new OmpResolution(OmpResolutionSource.Managed, executable, "The managed omp " + version + " is active and passed its version probe.");
        }

        notes.Add("The managed omp " + version + " did not pass its version probe.");
        return null;
    }
}
