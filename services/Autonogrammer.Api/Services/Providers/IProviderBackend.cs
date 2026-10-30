using Autonogrammer.Harness.OmpConfig;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>
/// The service's access to omp's provider state: what it holds and the writes the operator allows. The default implementation drives omp's
/// own commands and its <c>models.yml</c> (<see cref="OmpProviderBackend"/>); tests replace it. No member puts a key in a message or a log.
/// </summary>
internal interface IProviderBackend
{
    /// <summary>Reads what omp holds. A failure to read is reported as an incomplete state, not as an exception.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state.</returns>
    Task<ProviderBackendState> ReadStateAsync(CancellationToken cancellationToken);

    /// <summary>Hands a key to omp, which keeps it in its own credential database. The key is passed once and not kept.</summary>
    /// <param name="ompProviderId">The omp provider id.</param>
    /// <param name="apiKey">The key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The result.</returns>
    Task<ProviderWriteResult> ImportCredentialAsync(string ompProviderId, string apiKey, CancellationToken cancellationToken);

    /// <summary>Writes or replaces an Autonogrammer block of <c>models.yml</c>.</summary>
    /// <param name="provider">The block. It holds the name of the key variable, never a key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The result.</returns>
    Task<ProviderWriteResult> UpsertBlockAsync(OmpManagedProvider provider, CancellationToken cancellationToken);

    /// <summary>Removes an Autonogrammer block.</summary>
    /// <param name="providerId">The provider id.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The result.</returns>
    Task<ProviderWriteResult> RemoveBlockAsync(string providerId, CancellationToken cancellationToken);

    /// <summary>Makes a model the default for new chats.</summary>
    /// <param name="selector">The model as <c>provider/model</c>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The result.</returns>
    Task<ProviderWriteResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken);
}
