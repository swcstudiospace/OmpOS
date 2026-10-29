using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Api.Services.Providers;
using Autonogrammer.Contracts.Providers;
using Autonogrammer.Harness.OmpConfig;
using Autonogrammer.Harness.Providers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using ProviderKind = Autonogrammer.Contracts.Providers.ProviderKind;

namespace Autonogrammer.Api.Endpoints;

/// <summary>
/// The provider endpoints (PX-19, PX-10). <c>GET /providers</c> and <c>GET /providers/{id}</c> read the 103-row catalogue with whether the
/// service has a key for each row; <c>PUT /providers/{id}/credential</c> hands a key to omp; <c>PUT /models/default</c> sets the default model;
/// <c>POST /providers/custom</c> and <c>DELETE /providers/custom/{id}</c> manage the caller's custom endpoints; <c>POST /providers/{id}/test</c>
/// asks whether something answers at a provider address. Keys are operator-managed (OQ-104): credential writes answer 403
/// <c>credential_writes_disabled</c>, and configuration writes 403 <c>config_writes_disabled</c>, unless the operator turns them on, and a test
/// answers <c>skipped</c> unless the operator allows live calls (OQ-71). A key is write-only: no response, problem or log repeats it.
/// </summary>
internal static class ProvidersEndpoints
{
    /// <summary>Gets the rate-limiter policy name of the provider endpoints (single global bucket).</summary>
    public const string RateLimitPolicy = "Providers";

    private const int MaxKeyLength = 512;

    /// <summary>Maps the provider endpoints.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(string.Empty).WithTags("Providers").RequireRateLimiting(RateLimitPolicy);
        group.MapGet("/providers", ListAsync)
            .WithSummary("Lists the provider catalogue, filtered by region, kind and search text, with whether the service has a key for each provider. No key is ever returned.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        group.MapGet("/providers/{id}", GetAsync)
            .WithSummary("Reads one catalogue provider with whether the service has a key for it.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        group.MapPut("/providers/{id}/credential", SetCredentialAsync)
            .WithSummary("Hands an API key to omp. Write-only; disabled unless the operator sets Providers:AllowCredentialWrite.")
            .Accepts<SetCredentialRequest>("application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapPut("/models/default", SetDefaultModelAsync)
            .WithSummary("Makes a model the default for new chats. Disabled unless the operator sets Providers:AllowConfigWrite.")
            .Accepts<SetDefaultModelRequest>("application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/providers/custom", AddCustomAsync)
            .WithSummary("Adds an OpenAI- or Anthropic-compatible endpoint to omp. https is required except on this machine; link-local and metadata addresses and user information are refused. Disabled unless the operator sets Providers:AllowConfigWrite.")
            .Accepts<CustomProviderRequest>("application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapDelete("/providers/custom/{id}", RemoveCustomAsync)
            .WithSummary("Removes a custom endpoint the caller added. Disabled unless the operator sets Providers:AllowConfigWrite.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/providers/{id}/test", TestAsync)
            .WithSummary("Asks whether something answers at the provider's address. Sends no key; skipped unless the operator sets Providers:AllowLiveTest.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        return endpoints;
    }

    private static async Task<Results<Ok<ProvidersResponse>, ProblemHttpResult>> ListAsync(
        string? region,
        string? kind,
        string? search,
        ProviderStateReader reader,
        CustomProviderLedger ledger,
        IOptions<ProvidersOptions> options,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var filter = ParseFilter(region, kind, search, out var problem);
        if (filter is null)
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_filter", "Invalid filter.", problem!);
        }

        var state = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var summaries = CatalogueRows.All.Select(row => ProviderCatalogueMapper.ToSummary(row, reader.ConfiguredOf(row, state)));
        var settings = options.Value;
        return TypedResults.Ok(new ProvidersResponse
        {
            Providers = [.. summaries.Where(filter.Keeps)],
            Total = CatalogueRows.All.Count,
            Custom = [.. ledger.ListFor(user.Name).Select(record => ToInfo(record, reader.CustomState(record.KeyEnvName, state)))],
            OmpInstalled = state.OmpInstalled,
            CredentialWritesEnabled = settings.AllowCredentialWrite,
            ConfigWritesEnabled = settings.AllowConfigWrite,
            LiveTestsEnabled = settings.AllowLiveTest,
            DefaultModel = state.DefaultModel,
        });
    }

    private static async Task<Results<Ok<ProviderSummary>, ProblemHttpResult>> GetAsync(string id, ProviderStateReader reader, CancellationToken cancellationToken)
    {
        var row = CatalogueRows.Find(id);
        if (row is null)
        {
            return NotFound();
        }

        var state = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(ProviderCatalogueMapper.ToSummary(row, reader.ConfiguredOf(row, state)));
    }

    private static async Task<Results<Ok<CredentialResponse>, ProblemHttpResult>> SetCredentialAsync(
        HttpContext context,
        string id,
        IProviderBackend backend,
        ProviderStateReader reader,
        IOptions<ProvidersOptions> options,
        IOptions<ApiOptions> apiOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!options.Value.AllowCredentialWrite)
        {
            return WebProblems.Of(StatusCodes.Status403Forbidden, "credential_writes_disabled", "Credential writes are disabled.", ProviderCopy.CredentialWritesDisabled);
        }

        var row = CatalogueRows.Find(id);
        if (row is null)
        {
            return NotFound();
        }

        var body = await WebBodyReader.ReadAsync(context, apiOptions.Value, ProvidersJsonContext.Default.SetCredentialRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var request = body.Value!;
        var key = request.ApiKey?.Trim() ?? string.Empty;
        if (!IsAcceptableKey(key))
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_credential", "Invalid credential.", $"The apiKey must be non-blank, at most {MaxKeyLength} characters and hold no whitespace.");
        }

        var refusal = CredentialRefusal(row, request.PlanRuleAccepted);
        if (refusal is not null)
        {
            return refusal;
        }

        var result = await backend.ImportCredentialAsync(row.OmpProviderId!, key, cancellationToken).ConfigureAwait(false);
        reader.Invalidate();
        if (!result.IsDone)
        {
            return Failed(result, "credential_rejected", "The credential was refused.", key);
        }

        return TypedResults.Ok(new CredentialResponse { ProviderId = row.Id, Configured = ProviderConfiguredState.Configured });
    }

    private static ProblemHttpResult? CredentialRefusal(CatalogueRow row, bool planRuleAccepted)
    {
        if (row.Routes.Count == 0)
        {
            return WebProblems.Of(StatusCodes.Status409Conflict, "credential_not_applicable", "No key to write.", SignInSentence(row));
        }

        if (row.Routes.All(static route => route.Auth == CatalogueAuthStyle.None))
        {
            return WebProblems.Of(StatusCodes.Status409Conflict, "credential_not_applicable", "No key to write.", "This provider needs no key.");
        }

        if (row.Plan is not null && !planRuleAccepted)
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "plan_rule_not_accepted", "Plan rule not confirmed.", "Confirm that you have read the vendor's rule text for this plan (planRuleAccepted) before a key is written.");
        }

        if (!row.IsOmpNative || row.UrlVariables.Count > 0)
        {
            var variable = row.TakesApiKey ? ProviderKeyNames.EnvNameFor(row.BlockProviderId(row.Routes[0])) : null;
            return WebProblems.Of(StatusCodes.Status409Conflict, "credential_operator_managed", "Key set by the operator.", ProviderCopy.OperatorSetsKey(variable));
        }

        return null;
    }

    private static string SignInSentence(CatalogueRow row) =>
        string.IsNullOrWhiteSpace(row.LoginCommand)
            ? "This provider signs in through omp, not with a key."
            : $"This provider signs in through omp, not with a key. In omp, run {row.LoginCommand}.";

    private static async Task<Results<Ok<DefaultModelResponse>, ProblemHttpResult>> SetDefaultModelAsync(
        HttpContext context,
        IProviderBackend backend,
        ProviderStateReader reader,
        IOptions<ProvidersOptions> options,
        IOptions<ApiOptions> apiOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!options.Value.AllowConfigWrite)
        {
            return ConfigWritesDisabled();
        }

        var body = await WebBodyReader.ReadAsync(context, apiOptions.Value, ProvidersJsonContext.Default.SetDefaultModelRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var model = body.Value!.Model?.Trim();
        if (!OmpDefaultModel.IsValidSelector(model))
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_model", "Invalid model.", "The model must be written as provider/model.");
        }

        var result = await backend.SetDefaultModelAsync(model!, cancellationToken).ConfigureAwait(false);
        reader.Invalidate();
        return result.IsDone
            ? TypedResults.Ok(new DefaultModelResponse { Model = model! })
            : Failed(result, "model_rejected", "The model was refused.", null);
    }

    private static async Task<Results<Ok<CustomProviderInfo>, ProblemHttpResult>> AddCustomAsync(
        HttpContext context,
        IProviderBackend backend,
        ProviderStateReader reader,
        CustomProviderLedger ledger,
        IOptions<ProvidersOptions> options,
        IOptions<ApiOptions> apiOptions,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(user);
        if (!options.Value.AllowConfigWrite)
        {
            return ConfigWritesDisabled();
        }

        var body = await WebBodyReader.ReadAsync(context, apiOptions.Value, ProvidersJsonContext.Default.CustomProviderRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var checkedRequest = CustomProviderRequestValidator.Validate(body.Value!, user.Name);
        if (checkedRequest.Record is not { } record)
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_custom_provider", "Invalid custom endpoint.", checkedRequest.Problem!);
        }

        if (CustomProviderRequestValidator.IsReserved(record.Id) || (ledger.Find(record.Id) is { } other && !string.Equals(other.Owner, record.Owner, StringComparison.Ordinal)))
        {
            return WebProblems.Of(StatusCodes.Status409Conflict, "provider_id_taken", "Provider id taken.", "A provider with this id already exists. Choose another id.");
        }

        var result = await backend.UpsertBlockAsync(ToBlock(record), cancellationToken).ConfigureAwait(false);
        reader.Invalidate();
        if (!result.IsDone)
        {
            return Failed(result, "custom_provider_rejected", "The endpoint was refused.", null);
        }

        if (!ledger.TrySet(record))
        {
            return WebProblems.Of(StatusCodes.Status409Conflict, "provider_id_taken", "Provider id taken.", "A provider with this id already exists. Choose another id.");
        }

        var state = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(ToInfo(record, reader.CustomState(record.KeyEnvName, state)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveCustomAsync(
        string id,
        IProviderBackend backend,
        ProviderStateReader reader,
        CustomProviderLedger ledger,
        IOptions<ProvidersOptions> options,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!options.Value.AllowConfigWrite)
        {
            return ConfigWritesDisabled();
        }

        if (ledger.Find(id) is not { } record || !string.Equals(record.Owner, user.Name, StringComparison.Ordinal))
        {
            return NotFound();
        }

        var result = await backend.RemoveBlockAsync(id, cancellationToken).ConfigureAwait(false);
        reader.Invalidate();
        if (!result.IsDone)
        {
            return Failed(result, "custom_provider_rejected", "The endpoint was not removed.", null);
        }

        _ = ledger.TryRemove(user.Name, id);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ProviderTestResponse>, ProblemHttpResult>> TestAsync(
        string id,
        IProviderProbe probe,
        CustomProviderLedger ledger,
        IOptions<ProvidersOptions> options,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var target = ResolveTarget(id, ledger, user.Name, out var known, out var skipReason);
        if (!known)
        {
            return NotFound();
        }

        if (!options.Value.AllowLiveTest)
        {
            return TypedResults.Ok(Skipped(id, ProviderCopy.LiveTestsDisabled));
        }

        if (target is null)
        {
            return TypedResults.Ok(Skipped(id, skipReason ?? "This provider has no address to test."));
        }

        var found = await probe.ProbeAsync(target, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new ProviderTestResponse { ProviderId = id, Status = found.Status, Reason = found.Reason });
    }

    private static Uri? ResolveTarget(string id, CustomProviderLedger ledger, string owner, out bool known, out string? skipReason)
    {
        skipReason = null;
        if (CatalogueRows.Find(id) is { } row)
        {
            known = true;
            if (row.DefaultRoute is not { } route)
            {
                skipReason = "This provider signs in through omp and has no address to test.";
                return null;
            }

            if (CatalogueUrlTemplate.VariablesIn(route.BaseAddressTemplate).Count > 0)
            {
                skipReason = "The address of this provider holds placeholders that only an account can fill.";
                return null;
            }

            return Uri.TryCreate(route.BaseAddressTemplate, UriKind.Absolute, out var url) ? url : null;
        }

        var custom = ledger.Find(id);
        known = custom is not null && string.Equals(custom.Owner, owner, StringComparison.Ordinal);
        return known ? new Uri(custom!.BaseUrl, UriKind.Absolute) : null;
    }

    private static ProviderTestResponse Skipped(string id, string reason) =>
        new() { ProviderId = id, Status = ProviderTestStatus.Skipped, Reason = reason };

    private static ProviderCatalogueFilter? ParseFilter(string? region, string? kind, string? search, out string? problem)
    {
        problem = null;
        ProviderRegion? parsedRegion = null;
        if (!string.IsNullOrWhiteSpace(region))
        {
            if (!ProviderCatalogueFilter.TryParseRegion(region.Trim(), out var value))
            {
                problem = "The region must be one of europe, united-states, china, global or local.";
                return null;
            }

            parsedRegion = value;
        }

        ProviderKind? parsedKind = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!ProviderCatalogueFilter.TryParseKind(kind.Trim(), out var value))
            {
                problem = "The kind must be one of api, aggregator, coding-plan or local.";
                return null;
            }

            parsedKind = value;
        }

        if (search is not null && search.Length > ProviderCatalogueFilter.MaxSearchLength)
        {
            problem = $"The search text must be at most {ProviderCatalogueFilter.MaxSearchLength} characters.";
            return null;
        }

        return new ProviderCatalogueFilter(parsedRegion, parsedKind, search);
    }

    private static OmpManagedProvider ToBlock(CustomProviderRecord record) =>
        new(
            record.Id,
            new Uri(record.BaseUrl, UriKind.Absolute),
            record.Protocol == CustomProviderProtocol.Anthropic ? OmpProviderApi.AnthropicMessages : OmpProviderApi.OpenAiCompletions,
            record.KeyEnvName,
            NoAuthentication: record.KeyEnvName is null,
            [.. record.ModelIds.Select(static model => new OmpManagedModel(model))]);

    private static CustomProviderInfo ToInfo(CustomProviderRecord record, ProviderConfiguredState configured) => new()
    {
        Id = record.Id,
        DisplayName = record.DisplayName,
        Protocol = record.Protocol,
        BaseAddress = record.BaseUrl,
        ModelIds = record.ModelIds,
        KeyEnvName = record.KeyEnvName,
        Configured = configured,
    };

    private static bool IsAcceptableKey(string key) =>
        key.Length is > 0 and <= MaxKeyLength && !key.Any(static character => char.IsControl(character) || char.IsWhiteSpace(character));

    private static ProblemHttpResult NotFound() =>
        WebProblems.Of(StatusCodes.Status404NotFound, "provider_not_found", "Provider not found.", "No such provider.");

    private static ProblemHttpResult ConfigWritesDisabled() =>
        WebProblems.Of(StatusCodes.Status403Forbidden, "config_writes_disabled", "Configuration writes are disabled.", ProviderCopy.ConfigWritesDisabled);

    private static ProblemHttpResult Failed(ProviderWriteResult result, string code, string title, string? key)
    {
        var detail = result.Message ?? ProviderCopy.OmpRejected;
        if (!string.IsNullOrEmpty(key))
        {
            detail = detail.Replace(key, "<key>", StringComparison.Ordinal);
        }

        return result.Status switch
        {
            ProviderWriteStatus.Unavailable => WebProblems.Of(StatusCodes.Status503ServiceUnavailable, "omp_unavailable", "omp is unavailable.", detail),
            ProviderWriteStatus.Conflict => WebProblems.Of(StatusCodes.Status409Conflict, "provider_id_taken", "Provider id taken.", detail),
            _ => WebProblems.Of(StatusCodes.Status422UnprocessableEntity, code, title, detail),
        };
    }
}
