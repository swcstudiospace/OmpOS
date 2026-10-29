using Autonogrammer.Contracts.Providers;
using Autonogrammer.Harness.Providers;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>
/// Works out, for each catalogue row and custom endpoint, whether the service has what it needs: a credential omp holds, or the
/// environment variable of a configured block. The state of omp is read at most once per <see cref="ProvidersOptions.StateCacheSeconds"/>
/// seconds (it takes a process per read) and again after every write. It says a key is present, never that it works.
/// </summary>
internal sealed class ProviderStateReader : IDisposable
{
    private readonly IProviderBackend _backend;
    private readonly IProviderEnvironment _environment;
    private readonly ProvidersOptions _options;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ProviderBackendState? _cached;
    private DateTimeOffset _cachedAt;
    private int _cachedGeneration;
    private int _generation;

    /// <summary>Initializes a new instance of the <see cref="ProviderStateReader"/> class.</summary>
    /// <param name="backend">The access to omp.</param>
    /// <param name="environment">The environment of the service process.</param>
    /// <param name="options">The provider options.</param>
    /// <param name="clock">The clock.</param>
    public ProviderStateReader(IProviderBackend backend, IProviderEnvironment environment, IOptions<ProvidersOptions> options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _backend = backend;
        _environment = environment;
        _options = options.Value;
        _clock = clock;
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    /// <summary>Reads the state of omp, reusing a recent read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state.</returns>
    public async Task<ProviderBackendState> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _clock.GetUtcNow();
            var generation = Volatile.Read(ref _generation);
            if (_cached is not null && _cachedGeneration == generation && now - _cachedAt < TimeSpan.FromSeconds(_options.StateCacheSeconds))
            {
                return _cached;
            }

            _cached = await _backend.ReadStateAsync(cancellationToken).ConfigureAwait(false);
            _cachedAt = now;
            _cachedGeneration = generation;
            return _cached;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Drops the cached state, so the next read asks omp again. Call it after every write.</summary>
    public void Invalidate() => _ = Interlocked.Increment(ref _generation);

    /// <summary>Tells whether the service has a key or sign-in for a catalogue row.</summary>
    /// <param name="row">The row.</param>
    /// <param name="state">The state of omp.</param>
    /// <returns>The state of the row.</returns>
    public ProviderConfiguredState ConfiguredOf(CatalogueRow row, ProviderBackendState state)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(state);
        if (row.Routes.Count > 0 && row.Routes.All(static route => route.Auth == CatalogueAuthStyle.None))
        {
            return ProviderConfiguredState.NotRequired;
        }

        if (!state.OmpInstalled)
        {
            return ProviderConfiguredState.Unknown;
        }

        var held = row.OmpProviderId is not null && state.CredentialedProviderIds.Contains(row.OmpProviderId);
        if (row.Routes.Count == 0)
        {
            return Resolve(held, state.Complete);
        }

        var variableSet = row.OmpProviderId is not null && row.ApiKeyEnv is not null && _environment.IsSet(row.ApiKeyEnv);
        var blockSet = row.Routes.Any(route =>
            state.Blocks.TryGetValue(row.BlockProviderId(route), out var envName) && envName is not null && _environment.IsSet(envName));
        return Resolve(held || variableSet || blockSet, state.Complete || row.OmpProviderId is null);
    }

    /// <summary>Tells whether the service has the key of a custom endpoint.</summary>
    /// <param name="envName">The key variable, or null for a keyless endpoint.</param>
    /// <param name="state">The state of omp.</param>
    /// <returns>The state of the endpoint.</returns>
    public ProviderConfiguredState CustomState(string? envName, ProviderBackendState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (envName is null)
        {
            return ProviderConfiguredState.NotRequired;
        }

        if (!state.OmpInstalled)
        {
            return ProviderConfiguredState.Unknown;
        }

        return _environment.IsSet(envName) ? ProviderConfiguredState.Configured : ProviderConfiguredState.NotConfigured;
    }

    private static ProviderConfiguredState Resolve(bool configured, bool listingComplete) =>
        configured
            ? ProviderConfiguredState.Configured
            : listingComplete ? ProviderConfiguredState.NotConfigured : ProviderConfiguredState.Unknown;
}
