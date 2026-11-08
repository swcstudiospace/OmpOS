using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Presentation.Landing;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// Reads the status of omp on this computer for the Code landing: which source supplied it (managed, configured or PATH) and the version the probe
/// reports. It is the one place the landing's omp status is computed; the window only asks for it. With no managed root the managed install is not
/// consulted, and the status comes from the configured path and PATH alone.
/// </summary>
internal sealed class LocalOmpStatusProbe : IDisposable
{
    private static readonly Uri RegistryBaseUri = new("https://registry.npmjs.org/");
    private static readonly Uri GitHubBaseUri = new("https://github.com/");

    private readonly string? _configuredPath;
    private readonly OmpLocatorOptions _locator;
    private readonly OmpManagedOptions? _managed;
    private readonly HttpClient? _http;

    /// <summary>Creates the probe.</summary>
    /// <param name="configuredPath">The absolute omp path from configuration, or null.</param>
    /// <param name="managedRoot">The absolute directory the managed install owns, or null when the managed install is not consulted.</param>
    /// <param name="clock">The clock of the managed install.</param>
    /// <param name="locator">The PATH directories and known install locations, or null for the process environment. Tests pass their own directories.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
    public LocalOmpStatusProbe(string? configuredPath, string? managedRoot, TimeProvider clock, OmpLocatorOptions? locator = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _configuredPath = configuredPath;
        _locator = locator ?? OmpLocatorOptions.FromEnvironment(configuredPath: null);
        if (managedRoot is not null)
        {
            _http = new HttpClient();
            _managed = new OmpManagedOptions(
                managedRoot,
                RegistryBaseUri,
                GitHubBaseUri,
                _http,
                new OmpSystemProcessRunner(),
                new OmpSystemFileSystem(),
                clock);
        }
    }

    /// <summary>Reads the status of omp on a thread-pool thread, so the file checks and the probes never block the window.</summary>
    /// <param name="cancellationToken">Cancels the hash, the version probes and the wait for the process.</param>
    /// <returns>The status: installed with its source and version, not installed with the guidance, or the reason the probe threw.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<LocalOmpStatus> ReadAsync(CancellationToken cancellationToken) =>
        Task.Run(() => ReadCoreAsync(cancellationToken), cancellationToken);

    /// <summary>Releases the HTTP client of the managed install.</summary>
    public void Dispose() => _http?.Dispose();

    private async Task<LocalOmpStatus> ReadCoreAsync(CancellationToken cancellationToken)
    {
        var (source, executable) = _managed is null
            ? Unmanaged()
            : await ResolveManagedAsync(_managed, cancellationToken).ConfigureAwait(false);
        if (executable is null)
        {
            return LocalOmpStatus.NotInstalled(OmpLocator.InstallGuidance);
        }

        var probe = await OmpVersionProbe.ProbeAsync(executable, cancellationToken).ConfigureAwait(false);
        var version = probe is OmpVersionReported reported ? reported.Version : null;
        return LocalOmpStatus.Installed(version, SourceOf(source));
    }

    private async Task<(OmpResolutionSource Source, string? Executable)> ResolveManagedAsync(OmpManagedOptions managed, CancellationToken cancellationToken)
    {
        var resolution = await OmpResolution.ResolveAsync(managed, _configuredPath, _locator, cancellationToken).ConfigureAwait(false);
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
