namespace OmpOS.Presentation.Settings.Models;

/// <summary>
/// The Models page's access to omp. omp owns models, providers and credentials; a head implements this interface over omp's own
/// mechanisms so that every action here changes what omp uses. Expected failures are reported as a failed
/// <see cref="ModelsActionResult"/> or as a <see cref="SettingsSourceException"/> with text for the user; any other exception is a defect.
/// No member may put a key in a message, a log or an argument list of a process.
/// </summary>
public interface IModelsSettingsSource
{
    /// <summary>Reads the providers, the default model and omp's configuration warning.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="SettingsSourceException">omp's state could not be read.</exception>
    Task<ModelsSnapshot> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Hands an API key to omp, which keeps it in its own credential database. The key is passed once and not kept.</summary>
    /// <param name="providerId">The provider the key is for.</param>
    /// <param name="apiKey">The key, trimmed.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The result of the write.</returns>
    Task<ModelsActionResult> SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken);

    /// <summary>Makes a model the default for new chats.</summary>
    /// <param name="selector">The model as <c>provider/model</c>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The result of the write.</returns>
    Task<ModelsActionResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken);

    /// <summary>Adds a custom provider, or replaces the OmpOS managed one with the same id.</summary>
    /// <param name="form">The validated definition.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The result of the write.</returns>
    Task<ModelsActionResult> SaveCustomProviderAsync(CustomProviderForm form, CancellationToken cancellationToken);

    /// <summary>Removes an OmpOS managed custom provider.</summary>
    /// <param name="providerId">The provider to remove.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The result of the write.</returns>
    Task<ModelsActionResult> RemoveCustomProviderAsync(string providerId, CancellationToken cancellationToken);

    /// <summary>Tests one provider. Local providers are probed; a provider the source cannot test without a credential it lacks returns <see cref="ProviderTestStatus.NotTestable"/>.</summary>
    /// <param name="providerId">The provider to test.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>The outcome.</returns>
    Task<ProviderTestOutcome> TestAsync(string providerId, CancellationToken cancellationToken);
}
