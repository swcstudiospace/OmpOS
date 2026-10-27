using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Presentation.Tests.Settings.Models;

/// <summary>A scripted <see cref="IModelsSettingsSource"/> that offers provider listings and records what the cards hand it.</summary>
internal sealed class ListingModelsSource : IModelsSettingsSource
{
    private readonly List<ProviderInfo> _providers = [];

    /// <summary>Creates a source that offers <paramref name="listings"/>.</summary>
    public ListingModelsSource(params ProviderListing[] listings)
    {
        Listings = [.. listings];
    }

    /// <summary>Gets the listings the source offers; a test may change them before the next load.</summary>
    public List<ProviderListing> Listings { get; }

    /// <summary>Gets the number of loads.</summary>
    public int LoadCalls { get; private set; }

    /// <summary>Gets the keys handed to omp itself, with the omp provider id each was for.</summary>
    public List<(string ProviderId, string Key)> OmpKeys { get; } = [];

    /// <summary>Gets the catalogue requests and the key handed with each.</summary>
    public List<(CatalogueProviderRequest Request, string Key)> CatalogueAdds { get; } = [];

    /// <summary>Gets or sets the result of a catalogue add.</summary>
    public ModelsActionResult AddResult { get; set; } = ModelsActionResult.Success;

    /// <summary>Gets or sets whether a failed add quotes the key in its message (a faulty source).</summary>
    public bool EchoKeyInFailure { get; set; }

    /// <summary>Gets or sets a gate an add waits for.</summary>
    public TaskCompletionSource? AddGate { get; set; }

    /// <inheritdoc/>
    public Task<ModelsSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCalls++;
        return Task.FromResult(new ModelsSnapshot([.. _providers], null, null, true, [], [.. Listings]));
    }

    /// <inheritdoc/>
    public Task<ModelsActionResult> SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken)
    {
        OmpKeys.Add((providerId, apiKey));
        _providers.Add(Provider(providerId, CredentialState.Present));
        return Task.FromResult(ModelsActionResult.Success);
    }

    /// <inheritdoc/>
    public async Task<ModelsActionResult> AddCatalogueProviderAsync(CatalogueProviderRequest request, string apiKey, CancellationToken cancellationToken)
    {
        CatalogueAdds.Add((request, apiKey));
        if (AddGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        if (!AddResult.Succeeded)
        {
            return EchoKeyInFailure ? ModelsActionResult.Failure($"omp refused the key {apiKey}.") : AddResult;
        }

        var listing = Listings.Single(candidate => string.Equals(candidate.Id, request.ListingId, StringComparison.Ordinal));
        var first = listing.Routes[0].Id;
        var blockId = string.Equals(first, request.RouteId, StringComparison.Ordinal) ? listing.Id : $"{listing.Id}-{request.RouteId}";
        _providers.Add(Provider(blockId, apiKey.Length > 0 ? CredentialState.Present : CredentialState.NotRequired));
        return ModelsActionResult.Success;
    }

    /// <inheritdoc/>
    public Task<ModelsActionResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken) => Task.FromResult(ModelsActionResult.Success);

    /// <inheritdoc/>
    public Task<ModelsActionResult> SaveCustomProviderAsync(CustomProviderForm form, CancellationToken cancellationToken) => Task.FromResult(ModelsActionResult.Success);

    /// <inheritdoc/>
    public Task<ModelsActionResult> RemoveCustomProviderAsync(string providerId, CancellationToken cancellationToken) => Task.FromResult(ModelsActionResult.Success);

    /// <inheritdoc/>
    public Task<ProviderTestOutcome> TestAsync(string providerId, CancellationToken cancellationToken) =>
        throw new SettingsSourceException("Not scripted.");

    private static ProviderInfo Provider(string id, CredentialState credential) => new(
        id,
        id,
        ProviderGroup.Cloud,
        string.Empty,
        ProviderAuthKind.ApiKey,
        credential,
        [],
        null,
        null,
        null,
        false,
        null);
}
