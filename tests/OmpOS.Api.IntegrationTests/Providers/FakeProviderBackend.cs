using OmpOS.Api.Services.Providers;
using OmpOS.Harness.OmpConfig;

namespace OmpOS.Api.IntegrationTests.Providers;

/// <summary>A scripted omp: a state to report and a result for each write, recording what it was asked. It keeps the keys it receives so a test can prove a key arrived once and nowhere else.</summary>
internal sealed class FakeProviderBackend : IProviderBackend
{
    /// <summary>Gets or sets the state every read reports.</summary>
    public ProviderBackendState State { get; set; } = Installed();

    /// <summary>Gets or sets the result of the next credential import.</summary>
    public ProviderWriteResult ImportResult { get; set; } = ProviderWriteResult.Success;

    /// <summary>Gets or sets the result of the next block write or removal.</summary>
    public ProviderWriteResult BlockResult { get; set; } = ProviderWriteResult.Success;

    /// <summary>Gets or sets the result of the next default model write.</summary>
    public ProviderWriteResult DefaultResult { get; set; } = ProviderWriteResult.Success;

    /// <summary>Gets the number of state reads.</summary>
    public int Reads { get; private set; }

    /// <summary>Gets the omp provider ids a credential was imported for, with the key received.</summary>
    public List<(string ProviderId, string Key)> Imports { get; } = [];

    /// <summary>Gets the blocks written.</summary>
    public List<OmpManagedProvider> Upserts { get; } = [];

    /// <summary>Gets the ids of the blocks removed.</summary>
    public List<string> Removals { get; } = [];

    /// <summary>Gets the default models written.</summary>
    public List<string> Defaults { get; } = [];

    /// <summary>Builds the state of a service with omp installed.</summary>
    /// <param name="credentialed">The omp ids omp holds a credential for.</param>
    /// <param name="blocks">The blocks of models.yml by id with their key variable.</param>
    /// <param name="complete">Whether omp could list its models.</param>
    /// <returns>The state.</returns>
    public static ProviderBackendState Installed(IEnumerable<string>? credentialed = null, IReadOnlyDictionary<string, string?>? blocks = null, bool complete = true) =>
        new(
            OmpInstalled: true,
            new HashSet<string>(credentialed ?? [], StringComparer.Ordinal),
            blocks ?? new Dictionary<string, string?>(StringComparer.Ordinal),
            DefaultModel: "deepseek/deepseek-chat",
            complete);

    /// <inheritdoc />
    public Task<ProviderBackendState> ReadStateAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(State);
    }

    /// <inheritdoc />
    public Task<ProviderWriteResult> ImportCredentialAsync(string ompProviderId, string apiKey, CancellationToken cancellationToken)
    {
        Imports.Add((ompProviderId, apiKey));
        return Task.FromResult(ImportResult);
    }

    /// <inheritdoc />
    public Task<ProviderWriteResult> UpsertBlockAsync(OmpManagedProvider provider, CancellationToken cancellationToken)
    {
        Upserts.Add(provider);
        return Task.FromResult(BlockResult);
    }

    /// <inheritdoc />
    public Task<ProviderWriteResult> RemoveBlockAsync(string providerId, CancellationToken cancellationToken)
    {
        Removals.Add(providerId);
        return Task.FromResult(BlockResult);
    }

    /// <inheritdoc />
    public Task<ProviderWriteResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken)
    {
        Defaults.Add(selector);
        return Task.FromResult(DefaultResult);
    }
}
