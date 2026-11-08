using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// The launcher's installer over the managed root. Its operations run one at a time, so a check that is still reading the registry and a user's
/// install never write the same state record at once. It owns the HTTP client the operations use.
/// </summary>
internal sealed class OmpManagedInstall : IOmpManagedInstall, IDisposable
{
    private static readonly Uri RegistryBaseUri = new("https://registry.npmjs.org/");
    private static readonly Uri GitHubBaseUri = new("https://github.com/");

    private readonly HttpClient _http;
    private readonly OmpManagedInstaller _installer;
    private readonly OmpUpdateCheck _check;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the install over a managed root.</summary>
    /// <param name="rootDirectory">The absolute directory the managed install owns. Its <c>omp</c> child holds the install.</param>
    /// <param name="clock">The clock of the state record and the last-checked notice.</param>
    /// <exception cref="ArgumentException"><paramref name="rootDirectory"/> is not absolute.</exception>
    public OmpManagedInstall(string rootDirectory, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _http = new HttpClient();
        Options = new OmpManagedOptions(
            rootDirectory,
            RegistryBaseUri,
            GitHubBaseUri,
            _http,
            new OmpSystemProcessRunner(),
            new OmpSystemFileSystem(),
            clock);
        _installer = new OmpManagedInstaller(Options);
        _check = new OmpUpdateCheck(_installer);
    }

    /// <summary>Gets the options the install runs with. The status probe reads the same managed root through them.</summary>
    public OmpManagedOptions Options { get; }

    /// <inheritdoc />
    public Task<OmpUpdateResult> CheckForUpdateAsync(CancellationToken cancellationToken) =>
        SerializedAsync(() => _check.CheckForUpdateAsync(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<OmpRelease> ReadReleaseAsync(string? requestedVersion, CancellationToken cancellationToken) =>
        SerializedAsync(() => _installer.ReadReleaseAsync(requestedVersion, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<OmpUpdateResult> InstallAsync(OmpRelease release, CancellationToken cancellationToken) =>
        SerializedAsync(() => _installer.InstallAsync(release, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        _gate.Dispose();
        _http.Dispose();
    }

    private async Task<T> SerializedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
