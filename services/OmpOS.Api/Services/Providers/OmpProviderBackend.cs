using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Harness.OmpConfig;
using OmpOS.Harness.OmpLauncher;
using OmpOS.Harness.Secrets;

namespace OmpOS.Api.Services.Providers;

/// <summary>
/// The provider backend over omp itself: models come from <c>omp models --json</c>, a key goes in through <c>omp auth import</c>, the
/// default model through <c>omp config set</c>, and OmpOS blocks of <c>models.yml</c> are written by <see cref="OmpModelsFile"/> (timestamped
/// backup, atomic write, omp validates and the previous file is restored on refusal). Writes are serialised. omp is created on first use,
/// so a host without omp starts and reports <see cref="ProviderBackendState.NoOmp"/>.
/// </summary>
internal sealed class OmpProviderBackend : IProviderBackend, IDisposable
{
    private readonly ProvidersOptions _options;
    private readonly string? _harnessOmpPath;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private OmpModelConfiguration? _omp;

    /// <summary>Initializes a new instance of the <see cref="OmpProviderBackend"/> class.</summary>
    /// <param name="options">The provider options.</param>
    /// <param name="harness">The harness options, whose ACP omp path is the fallback.</param>
    /// <param name="clock">The clock.</param>
    public OmpProviderBackend(IOptions<ProvidersOptions> options, IOptions<HarnessOptions> harness, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(harness);
        ArgumentNullException.ThrowIfNull(clock);
        _options = options.Value;
        _harnessOmpPath = harness.Value.Acp.OmpPath;
        _clock = clock;
    }

    /// <inheritdoc />
    public void Dispose() => _writeGate.Dispose();

    /// <inheritdoc />
    public async Task<ProviderBackendState> ReadStateAsync(CancellationToken cancellationToken)
    {
        var omp = Configuration();
        if (omp is null)
        {
            return ProviderBackendState.NoOmp;
        }

        var listing = await omp.Catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var managed = await omp.ModelsFile.ReadManagedAsync(cancellationToken).ConfigureAwait(false);
        var roles = await omp.DefaultModel.GetAsync(cancellationToken).ConfigureAwait(false);
        var credentialed = listing.Models.Select(static model => model.ProviderId).ToHashSet(StringComparer.Ordinal);
        var blocks = managed.Providers.ToDictionary(static provider => provider.Id, static provider => provider.ApiKeyEnvName, StringComparer.Ordinal);
        return new ProviderBackendState(OmpInstalled: true, credentialed, blocks, roles.DefaultSelector, listing.IsComplete);
    }

    /// <inheritdoc />
    public async Task<ProviderWriteResult> ImportCredentialAsync(string ompProviderId, string apiKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ompProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var omp = Configuration();
        if (omp is null)
        {
            return new ProviderWriteResult(ProviderWriteStatus.Unavailable, ProviderCopy.OmpNotInstalled);
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var imported = await omp.Credentials.ImportApiKeyAsync(ompProviderId, new SecretString(apiKey), cancellationToken).ConfigureAwait(false);
            return imported.IsImported
                ? ProviderWriteResult.Success
                : new ProviderWriteResult(ProviderWriteStatus.Rejected, PathScrubber.WithoutPaths(imported.Message));
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ProviderWriteResult> UpsertBlockAsync(OmpManagedProvider provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var omp = Configuration();
        if (omp is null)
        {
            return new ProviderWriteResult(ProviderWriteStatus.Unavailable, ProviderCopy.OmpNotInstalled);
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Map(await omp.ModelsFile.UpsertAsync(provider, cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ProviderWriteResult> RemoveBlockAsync(string providerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        var omp = Configuration();
        if (omp is null)
        {
            return new ProviderWriteResult(ProviderWriteStatus.Unavailable, ProviderCopy.OmpNotInstalled);
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Map(await omp.ModelsFile.RemoveAsync(providerId, cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ProviderWriteResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        var omp = Configuration();
        if (omp is null)
        {
            return new ProviderWriteResult(ProviderWriteStatus.Unavailable, ProviderCopy.OmpNotInstalled);
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await omp.DefaultModel.SetDefaultAsync(selector, cancellationToken).ConfigureAwait(false);
            return result.IsSet
                ? ProviderWriteResult.Success
                : new ProviderWriteResult(ProviderWriteStatus.Rejected, PathScrubber.WithoutPaths(result.Message));
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    private static ProviderWriteResult Map(OmpModelsWriteResult write) => write.Status switch
    {
        OmpModelsWriteStatus.Written or OmpModelsWriteStatus.Unchanged => ProviderWriteResult.Success,
        OmpModelsWriteStatus.Conflict => new ProviderWriteResult(ProviderWriteStatus.Conflict, ProviderCopy.NotOwned),
        OmpModelsWriteStatus.Failed => new ProviderWriteResult(ProviderWriteStatus.Unavailable, Scrub(write.Message, ProviderCopy.OmpUnavailable)),
        _ => new ProviderWriteResult(ProviderWriteStatus.Rejected, Scrub(write.Message, ProviderCopy.OmpRejected)),
    };

    private static string Scrub(string? message, string fallback) =>
        string.IsNullOrWhiteSpace(message) ? fallback : PathScrubber.WithoutPaths(message);

    private OmpModelConfiguration? Configuration()
    {
        if (_omp is not null)
        {
            return _omp;
        }

        var configured = FirstAbsolute(_options.OmpPath, _harnessOmpPath);
        var locator = OmpLocatorOptions.FromEnvironment(configured);
        if (OmpLocator.Locate(locator) is not OmpFound)
        {
            return null;
        }

        _omp = new OmpModelConfiguration(new OmpConfigOptions
        {
            Locator = locator,
            AgentDirectory = string.IsNullOrWhiteSpace(_options.AgentDirectory) ? null : _options.AgentDirectory,
            TimeProvider = _clock,
        });
        return _omp;
    }

    private static string? FirstAbsolute(params string?[] paths) =>
        paths.FirstOrDefault(static path => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path));
}
