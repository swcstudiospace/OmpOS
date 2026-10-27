using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Presentation.Tests.Settings.Models;

/// <summary>A source written before catalogue support existed: it implements only the original members and so gets the default <see cref="IModelsSettingsSource.AddCatalogueProviderAsync"/>.</summary>
internal sealed class LegacyModelsSource : IModelsSettingsSource
{
    private readonly ProviderListing[] _listings;

    /// <summary>Creates a source that offers <paramref name="listings"/>.</summary>
    public LegacyModelsSource(params ProviderListing[] listings)
    {
        _listings = listings;
    }

    /// <inheritdoc/>
    public Task<ModelsSnapshot> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ModelsSnapshot([], null, null, true, [], _listings));

    /// <inheritdoc/>
    public Task<ModelsActionResult> SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken) => Task.FromResult(ModelsActionResult.Success);

    /// <inheritdoc/>
    public Task<ModelsActionResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken) => Task.FromResult(ModelsActionResult.Success);

    /// <inheritdoc/>
    public Task<ModelsActionResult> SaveCustomProviderAsync(CustomProviderForm form, CancellationToken cancellationToken) => Task.FromResult(ModelsActionResult.Success);

    /// <inheritdoc/>
    public Task<ModelsActionResult> RemoveCustomProviderAsync(string providerId, CancellationToken cancellationToken) => Task.FromResult(ModelsActionResult.Success);

    /// <inheritdoc/>
    public Task<ProviderTestOutcome> TestAsync(string providerId, CancellationToken cancellationToken) => throw new SettingsSourceException("Not scripted.");
}
