using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Presentation.Landing;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// Reads the status of omp on this computer for the Code landing, and resolves the omp a launch runs. The resolution is the launcher's: a configured
/// path that runs, then the managed install, then PATH and the known install locations. With no managed options the managed install is not
/// consulted, and the status comes from the configured path and PATH alone.
/// </summary>
internal sealed class LocalOmpStatusProbe
{
    private readonly string? _configuredPath;
    private readonly OmpLocatorOptions _locator;
    private readonly OmpManagedOptions? _managed;

    /// <summary>Creates the probe.</summary>
    /// <param name="configuredPath">The absolute omp path from configuration, or null.</param>
    /// <param name="managed">The managed options of the install, or null when the managed install is not consulted.</param>
    /// <param name="locator">The PATH directories and known install locations, or null for the process environment. Tests pass their own directories.</param>
    public LocalOmpStatusProbe(string? configuredPath, OmpManagedOptions? managed, OmpLocatorOptions? locator = null)
    {
        _configuredPath = configuredPath;
        _managed = managed;
        _locator = locator ?? OmpLocatorOptions.FromEnvironment(configuredPath: null);
    }

    /// <summary>Reads the status of omp on a thread-pool thread, so the file checks and the probes never block the window.</summary>
    /// <param name="cancellationToken">Cancels the hash, the version probes and the wait for the process.</param>
    /// <returns>
    /// The status: installed from the managed install, missing from the managed install but runnable from elsewhere, not installed with the guidance,
    /// or the reason the probe threw.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<LocalOmpStatus> ReadAsync(CancellationToken cancellationToken) =>
        Task.Run(() => ReadCoreAsync(cancellationToken), cancellationToken);

    /// <summary>Resolves the omp executable a launch runs, on a thread-pool thread.</summary>
    /// <param name="cancellationToken">Cancels the hash and the version probe.</param>
    /// <returns>The absolute path of the executable, or null when no source supplied a runnable omp.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<string?> ResolveExecutableAsync(CancellationToken cancellationToken) =>
        Task.Run(async () => (await ResolveCoreAsync(cancellationToken).ConfigureAwait(false)).Executable, cancellationToken);

    private async Task<LocalOmpStatus> ReadCoreAsync(CancellationToken cancellationToken)
    {
        var (source, executable) = await ResolveCoreAsync(cancellationToken).ConfigureAwait(false);
        if (executable is null)
        {
            return LocalOmpStatus.NotInstalled(OmpLocator.InstallGuidance);
        }

        var probe = await OmpVersionProbe.ProbeAsync(executable, cancellationToken).ConfigureAwait(false);
        var version = probe is OmpVersionReported reported ? reported.Version : null;
        var from = SourceOf(source);
        return _managed is not null && source != OmpResolutionSource.Managed
            ? LocalOmpStatus.Missing(version, from)
            : LocalOmpStatus.Installed(version, from);
    }

    private async Task<(OmpResolutionSource Source, string? Executable)> ResolveCoreAsync(CancellationToken cancellationToken)
    {
        if (_managed is null)
        {
            return Unmanaged();
        }

        var resolution = await OmpResolution.ResolveAsync(_managed, _configuredPath, _locator, cancellationToken).ConfigureAwait(false);
        return (resolution.Source, resolution.ExecutablePath);
    }

    private (OmpResolutionSource Source, string? Executable) Unmanaged()
    {
        if (OmpLocator.Locate(new OmpLocatorOptions(_configuredPath, _locator.SearchDirectories, _locator.KnownInstallLocations)) is OmpFound found)
        {
            var configured = _configuredPath is not null && string.Equals(found.ExecutablePath, _configuredPath, StringComparison.Ordinal);
            return (configured ? OmpResolutionSource.Configured : OmpResolutionSource.Path, found.ExecutablePath);
        }

        return (OmpResolutionSource.None, null);
    }

    private static LocalOmpSource SourceOf(OmpResolutionSource source) => source switch
    {
        OmpResolutionSource.Managed => LocalOmpSource.Managed,
        OmpResolutionSource.Configured => LocalOmpSource.Configured,
        _ => LocalOmpSource.Path,
    };
}
