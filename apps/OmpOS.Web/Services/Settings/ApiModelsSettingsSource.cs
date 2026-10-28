using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Models;
using Wire = OmpOS.Contracts.Providers;

namespace OmpOS.Web.Services.Settings;

/// <summary>
/// The Models page's source on the web: <see cref="IModelsSettingsSource"/> over the provider endpoints of the API. The service owns omp,
/// its keys and its configuration, and provider keys are operator-managed there: when the operator has not enabled key writes, this source
/// reports it as <see cref="CredentialWritesDisabledReason"/> (and as the snapshot warning) so the page disables the key field with that
/// sentence, and it never sends a key it knows the service will refuse. A key travels once, in the body of one request, and is not kept,
/// logged or echoed; a problem of the API becomes a failed <see cref="ModelsActionResult"/> with the sentence of the API, never the request.
/// </summary>
internal sealed class ApiModelsSettingsSource : IModelsSettingsSource
{
    private readonly HttpClient _http;
    private Dictionary<string, string> _listingByOmpId = new(StringComparer.Ordinal);
    private HashSet<string> _listingIds = new(StringComparer.Ordinal);
    private HashSet<string> _planListingIds = new(StringComparer.Ordinal);
    private readonly bool _isDemo;
    private bool? _credentialWritesEnabled;
    private bool? _configWritesEnabled;

    /// <summary>Initializes a new instance of the <see cref="ApiModelsSettingsSource"/> class.</summary>
    /// <param name="http">An HTTP client whose base address is the API origin and that sends the caller's bearer token.</param>
    /// <param name="isDemo">True when the API is the in-browser Demo handler: the disabled-key sentence then says so.</param>
    internal ApiModelsSettingsSource(HttpClient http, bool isDemo = false)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _isDemo = isDemo;
    }

    /// <summary>
    /// Gets the sentence that says the operator disabled key writes, or null when writes are enabled or the service was not asked yet. The
    /// web key field is disabled and shows this sentence while it is not null.
    /// </summary>
    internal string? CredentialWritesDisabledReason => _credentialWritesEnabled == false ? (_isDemo ? ModelsApiCopy.CredentialWritesDisabledInDemo : ModelsApiCopy.CredentialWritesDisabled) : null;

    /// <summary>Gets the sentence that says the operator disabled configuration writes, or null.</summary>
    internal string? ConfigWritesDisabledReason => _configWritesEnabled == false ? ModelsApiCopy.ConfigWritesDisabled : null;

    /// <inheritdoc />
    public async Task<ModelsSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(new Uri("providers", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new SettingsSourceException(ModelsApiCopy.Unreachable, exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var problem = await ReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
                throw new SettingsSourceException(Describe(problem));
            }

            Wire.ProvidersResponse? body;
            try
            {
                body = await BoundedContent.ReadJsonAsync(
                    response.Content,
                    Wire.ProvidersJsonContext.Default.ProvidersResponse,
                    BoundedContent.MaxSuccessBytes,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is JsonException or ApiClientException)
            {
                throw new SettingsSourceException(ModelsApiCopy.UnexpectedAnswer, exception);
            }

            return body is null ? throw new SettingsSourceException(ModelsApiCopy.UnexpectedAnswer) : Remember(body);
        }
    }

    /// <summary>
    /// Hands a key to the service for a listing that omp knows. The Models page refuses a coding plan until the user ticked that they read the
    /// vendor's rule text, before it calls this method, so a listing with a plan reaches here with the tick and the request carries it.
    /// </summary>
    /// <param name="providerId">The omp provider id of the listing.</param>
    /// <param name="apiKey">The key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The result.</returns>
    public Task<ModelsActionResult> SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        var listingId = ListingIdOf(providerId);
        return SaveApiKeyAsync(providerId, apiKey, listingId is not null && _planListingIds.Contains(listingId), cancellationToken);
    }

    /// <summary>Hands an API key to the service for a catalogue provider.</summary>
    /// <param name="providerId">The omp provider id of the listing, or its catalogue id.</param>
    /// <param name="apiKey">The key, trimmed.</param>
    /// <param name="planRuleAccepted">True when the user confirmed that they read the vendor's rule text. The service requires it for a coding plan.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The result. A refusal carries the sentence of the API.</returns>
    internal async Task<ModelsActionResult> SaveApiKeyAsync(string providerId, string apiKey, bool planRuleAccepted, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(apiKey);
        if (CredentialWritesDisabledReason is { } reason)
        {
            return ModelsActionResult.Failure(reason);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ModelsActionResult.Failure(ModelsCopy.ErrApiKeyRequired);
        }

        if (CustomProviderRules.ValidateApiKey(apiKey) is { } keyProblem)
        {
            return ModelsActionResult.Failure(keyProblem);
        }

        var listingId = ListingIdOf(providerId);
        if (listingId is null)
        {
            return ModelsActionResult.Failure(ModelsApiCopy.UnknownProvider);
        }

        var request = new Wire.SetCredentialRequest { ApiKey = apiKey.Trim(), PlanRuleAccepted = planRuleAccepted };
        return await SendAsync(
            () => _http.PutAsJsonAsync(new Uri($"providers/{Uri.EscapeDataString(listingId)}/credential", UriKind.Relative), request, Wire.ProvidersJsonContext.Default.SetCredentialRequest, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ModelsActionResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (ConfigWritesDisabledReason is { } reason)
        {
            return ModelsActionResult.Failure(reason);
        }

        var request = new Wire.SetDefaultModelRequest { Model = selector };
        return await SendAsync(
            () => _http.PutAsJsonAsync(new Uri("models/default", UriKind.Relative), request, Wire.ProvidersJsonContext.Default.SetDefaultModelRequest, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ModelsActionResult> SaveCustomProviderAsync(CustomProviderForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (ConfigWritesDisabledReason is { } reason)
        {
            return ModelsActionResult.Failure(reason);
        }

        var request = new Wire.CustomProviderRequest
        {
            Id = form.Id,
            DisplayName = form.DisplayName,
            Protocol = form.Protocol == CustomProviderProtocol.AnthropicCompatible ? Wire.CustomProviderProtocol.Anthropic : Wire.CustomProviderProtocol.OpenAi,
            BaseAddress = form.BaseEndpoint.AbsoluteUri,
            ModelIds = form.Models,
            KeyRequired = form.Auth != CustomProviderAuth.None,
        };
        return await SendAsync(
            () => _http.PostAsJsonAsync(new Uri("providers/custom", UriKind.Relative), request, Wire.ProvidersJsonContext.Default.CustomProviderRequest, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ModelsActionResult> RemoveCustomProviderAsync(string providerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        if (ConfigWritesDisabledReason is { } reason)
        {
            return ModelsActionResult.Failure(reason);
        }

        return await SendAsync(
            () => _http.DeleteAsync(new Uri($"providers/custom/{Uri.EscapeDataString(providerId)}", UriKind.Relative), cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ProviderTestOutcome> TestAsync(string providerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        var id = ListingIdOf(providerId) ?? providerId;
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(new Uri($"providers/{Uri.EscapeDataString(id)}/test", UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return new ProviderTestOutcome(ProviderTestStatus.Failed, ModelsApiCopy.Unreachable);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var problem = await ReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
                return new ProviderTestOutcome(ProviderTestStatus.NotTestable, Describe(problem));
            }

            Wire.ProviderTestResponse? body;
            try
            {
                body = await BoundedContent.ReadJsonAsync(
                    response.Content,
                    Wire.ProvidersJsonContext.Default.ProviderTestResponse,
                    BoundedContent.MaxSuccessBytes,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is JsonException or ApiClientException)
            {
                return new ProviderTestOutcome(ProviderTestStatus.Failed, ModelsApiCopy.UnexpectedAnswer);
            }

            return body is null
                ? new ProviderTestOutcome(ProviderTestStatus.Failed, ModelsApiCopy.UnexpectedAnswer)
                : new ProviderTestOutcome(ToStatus(body.Status), body.Reason ?? string.Empty);
        }
    }

    /// <inheritdoc />
    public Task<ModelsActionResult> AddCatalogueProviderAsync(CatalogueProviderRequest request, string apiKey, CancellationToken cancellationToken) =>
        Task.FromResult(ModelsActionResult.Failure(ModelsApiCopy.CatalogueNotSupported));

    private static ProviderTestStatus ToStatus(Wire.ProviderTestStatus status) => status switch
    {
        Wire.ProviderTestStatus.Reachable => ProviderTestStatus.Reachable,
        Wire.ProviderTestStatus.Unauthorized => ProviderTestStatus.Unauthorized,
        Wire.ProviderTestStatus.Unreachable => ProviderTestStatus.NotRunning,
        _ => ProviderTestStatus.NotTestable,
    };

    private static string Describe(ProviderProblem problem) => problem switch
    {
        { Status: (int)HttpStatusCode.Unauthorized } => ModelsApiCopy.SignInRequired,
        { Status: (int)HttpStatusCode.TooManyRequests } => ModelsApiCopy.TooManyRequests,
        { Code: "credential_writes_disabled" } => ModelsApiCopy.CredentialWritesDisabled,
        { Code: "config_writes_disabled" } => ModelsApiCopy.ConfigWritesDisabled,
        { Detail: { Length: > 0 } detail } => detail,
        _ => ModelsCopy.ActionFailed,
    };

    private static async Task<ProviderProblem> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? text;
        try
        {
            text = await BoundedContent.ReadTextAsync(response.Content, BoundedContent.MaxErrorBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (ApiClientException)
        {
            text = null;
        }

        return ProviderProblem.Parse((int)response.StatusCode, text);
    }

    private ModelsSnapshot Remember(Wire.ProvidersResponse body)
    {
        _credentialWritesEnabled = body.CredentialWritesEnabled;
        _configWritesEnabled = body.ConfigWritesEnabled;
        var byOmp = new Dictionary<string, string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var plans = new HashSet<string>(StringComparer.Ordinal);
        foreach (var summary in body.Providers)
        {
            _ = ids.Add(summary.Id);
            if (summary.Plan is not null)
            {
                _ = plans.Add(summary.Id);
            }

            if (summary.OmpProviderId is not null)
            {
                _ = byOmp.TryAdd(summary.OmpProviderId, summary.Id);
            }
        }

        _listingByOmpId = byOmp;
        _listingIds = ids;
        _planListingIds = plans;
        var cards = new List<ProviderInfo>();
        foreach (var summary in body.Providers)
        {
            if (ProviderSummaryMapper.ToConfiguredCard(summary) is { } card)
            {
                cards.Add(card);
            }
        }

        cards.AddRange(body.Custom.Select(ProviderSummaryMapper.ToCustomCard));
        return new ModelsSnapshot(
            cards,
            body.DefaultModel,
            WarningOf(body),
            body.OmpInstalled,
            [],
            [.. body.Providers.Select(ProviderSummaryMapper.ToListing)],
            CredentialWritesDisabledReason);
    }

    private static string? WarningOf(Wire.ProvidersResponse body)
    {
        var sentences = new List<string>(2);
        if (!body.OmpInstalled)
        {
            sentences.Add(ModelsApiCopy.OmpNotInstalled);
        }

        return sentences.Count == 0 ? null : string.Join(' ', sentences);
    }

    private string? ListingIdOf(string providerId)
    {
        if (_listingByOmpId.TryGetValue(providerId, out var byOmp))
        {
            return byOmp;
        }

        return _listingIds.Contains(providerId) ? providerId : null;
    }

    private static async Task<ModelsActionResult> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await send().ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return ModelsActionResult.Failure(ModelsApiCopy.Unreachable);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return ModelsActionResult.Success;
            }

            var problem = await ReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
            return ModelsActionResult.Failure(Describe(problem));
        }
    }
}
