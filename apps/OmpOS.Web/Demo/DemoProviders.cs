using OmpOS.Contracts.Providers;
using OmpOS.Harness.Providers;

namespace OmpOS.Web.Demo;

/// <summary>
/// The provider state of the Demo: the real catalogue (every EU, US, CN, global and local row) as the providers route lists it, the custom
/// endpoints a visitor adds in this tab, the default model, and the refusals the real route gives. Key entry is disabled by the Demo's operator
/// (no service holds a key), live tests are off, and configuration writes (custom endpoints, default model) work in memory only.
/// </summary>
internal sealed class DemoProviders
{
    /// <summary>The sentence of the 403 a key write gets.</summary>
    internal const string CredentialWritesDisabled = "Credential writes are disabled by the operator. This Demo holds no keys and sends nothing to a provider; a real service takes provider keys in its own environment.";

    /// <summary>The sentence of the skipped live test.</summary>
    internal const string LiveTestsDisabled = "Live tests are disabled in this Demo. No call was made.";

    private const string DefaultSelector = "anthropic/claude-sonnet-4-5";

    private static readonly string[] ConfiguredIds = ["anthropic"];

    private readonly Lock _gate = new();
    private readonly List<CustomProviderInfo> _custom = [];
    private string _defaultModel = DefaultSelector;

    /// <summary>Lists the catalogue through the filters.</summary>
    /// <param name="region">The <c>region</c> query value, or null.</param>
    /// <param name="kind">The <c>kind</c> query value, or null.</param>
    /// <param name="search">The <c>search</c> query value, or null.</param>
    /// <returns>The response.</returns>
    /// <exception cref="DemoProblemException">The region or kind is not a wire name (400 <c>invalid_filter</c>).</exception>
    internal ProvidersResponse List(string? region, string? kind, string? search)
    {
        var filter = DemoProvidersFilter.Parse(region, kind, search);
        lock (_gate)
        {
            return new ProvidersResponse
            {
                Providers = [.. CatalogueRows.All.Select(ToSummary).Where(filter.Keeps)],
                Total = CatalogueRows.All.Count,
                Custom = [.. _custom],
                OmpInstalled = true,
                CredentialWritesEnabled = false,
                ConfigWritesEnabled = true,
                LiveTestsEnabled = false,
                DefaultModel = _defaultModel,
            };
        }
    }

    /// <summary>Reads one provider.</summary>
    /// <param name="id">The catalogue id.</param>
    /// <returns>The summary.</returns>
    /// <exception cref="DemoProblemException">No such provider (404 <c>provider_not_found</c>).</exception>
    internal static ProviderSummary Get(string id) => ToSummary(Find(id));

    /// <summary>Refuses a key write: the Demo's operator disabled them.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="DemoProblemException">Always (403 <c>credential_writes_disabled</c>).</exception>
    internal static CredentialResponse RefuseCredential() =>
        throw new DemoProblemException(403, "credential_writes_disabled", "Credential writes are disabled.", CredentialWritesDisabled);

    /// <summary>Answers a live test: the Demo never calls a provider, so the test is skipped with its reason.</summary>
    /// <param name="id">The provider id: a catalogue id or a custom endpoint of this tab.</param>
    /// <returns>The skipped result.</returns>
    /// <exception cref="DemoProblemException">No such provider (404 <c>provider_not_found</c>).</exception>
    internal ProviderTestResponse Test(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_gate)
        {
            if (CatalogueRows.Find(id) is null && !_custom.Exists(custom => string.Equals(custom.Id, id, StringComparison.Ordinal)))
            {
                throw NotFound();
            }
        }

        return new ProviderTestResponse { ProviderId = id, Status = ProviderTestStatus.Skipped, Reason = LiveTestsDisabled };
    }

    /// <summary>Makes a model the default.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The response.</returns>
    /// <exception cref="DemoProblemException">The model is not written as provider/model (400 <c>invalid_model</c>).</exception>
    internal DefaultModelResponse SetDefault(SetDefaultModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var model = request.Model?.Trim();
        if (!IsSelector(model))
        {
            throw new DemoProblemException(400, "invalid_model", "Invalid model.", "The model must be written as provider/model.");
        }

        lock (_gate)
        {
            _defaultModel = model!;
        }

        return new DefaultModelResponse { Model = model! };
    }

    /// <summary>Adds a custom endpoint, validated like the real route.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The endpoint.</returns>
    /// <exception cref="DemoProblemException">The request is invalid (400 <c>invalid_custom_provider</c>) or the id is taken (409 <c>provider_id_taken</c>).</exception>
    internal CustomProviderInfo AddCustom(CustomProviderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var problem = DemoProvidersCustomRules.Validate(request, out var address, out var displayName, out var models);
        if (problem is not null)
        {
            throw new DemoProblemException(400, "invalid_custom_provider", "Invalid custom provider.", problem);
        }

        var id = request.Id!;
        lock (_gate)
        {
            if (DemoProvidersCustomRules.IsReserved(id) || _custom.Exists(custom => string.Equals(custom.Id, id, StringComparison.Ordinal)))
            {
                throw new DemoProblemException(409, "provider_id_taken", "Provider id taken.", "A provider with this id already exists. Choose another id.");
            }

            var keyed = request.KeyRequired ?? true;
            var added = new CustomProviderInfo
            {
                Id = id,
                DisplayName = displayName,
                Protocol = request.Protocol!.Value,
                BaseAddress = address,
                ModelIds = models,
                KeyEnvName = keyed ? DemoProvidersCustomRules.KeyEnvNameFor(id) : null,
                Configured = keyed ? ProviderConfiguredState.NotConfigured : ProviderConfiguredState.NotRequired,
            };
            _custom.Add(added);
            return added;
        }
    }

    /// <summary>Removes a custom endpoint of this tab.</summary>
    /// <param name="id">The endpoint id.</param>
    /// <exception cref="DemoProblemException">No such custom endpoint (404 <c>provider_not_found</c>).</exception>
    internal void RemoveCustom(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_gate)
        {
            if (_custom.RemoveAll(custom => string.Equals(custom.Id, id, StringComparison.Ordinal)) == 0)
            {
                throw NotFound();
            }
        }
    }

    private static DemoProblemException NotFound() => new(404, "provider_not_found", "Provider not found.", "No such provider.");

    private static CatalogueRow Find(string id) =>
        CatalogueRows.Find(id) ?? throw NotFound();

    private static ProviderSummary ToSummary(CatalogueRow row) => DemoProvidersMapper.ToSummary(row, ConfiguredOf(row));

    private static ProviderConfiguredState ConfiguredOf(CatalogueRow row)
    {
        if (row.Routes.Count > 0 && row.Routes.All(static route => route.Auth == CatalogueAuthStyle.None))
        {
            return ProviderConfiguredState.NotRequired;
        }

        return ConfiguredIds.Contains(row.Id, StringComparer.Ordinal) ? ProviderConfiguredState.Configured : ProviderConfiguredState.NotConfigured;
    }

    private static bool IsSelector(string? selector)
    {
        if (string.IsNullOrEmpty(selector) || selector.Length > 256)
        {
            return false;
        }

        var slash = selector.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0 || slash == selector.Length - 1)
        {
            return false;
        }

        var provider = selector[..slash];
        return char.IsAsciiLetterOrDigit(provider[0]) && provider.Length <= 64
            && provider.All(static character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
            && provider.All(static character => !char.IsAsciiLetterUpper(character))
            && selector.All(static character => !char.IsWhiteSpace(character) && !char.IsControl(character));
    }
}
