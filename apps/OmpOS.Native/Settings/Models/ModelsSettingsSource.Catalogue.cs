using OmpOS.Harness.OmpConfig;
using OmpOS.Harness.Providers;
using OmpOS.Harness.Secrets;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Settings.Models;

/// <summary>
/// The catalogue half of the Models source: adding one row of <see cref="CatalogueRows"/> to omp. A row omp knows by id on its
/// default route gets its key through <c>omp auth import</c> and no block; every other row or route gets an OmpOS block in
/// <c>models.yml</c> (written by <see cref="OmpModelsFile"/>: backup, atomic write, omp validates and the previous file is restored
/// on refusal) and its key in the OmpOS key store under <see cref="ProviderKeyEnvironment.EnvNameFor"/> of the block id. Removing a
/// block and choosing the default model are the existing members of the source; they work on a catalogue block because it is an
/// ordinary OmpOS block.
/// </summary>
internal sealed partial class ModelsSettingsSource
{
    /// <inheritdoc />
    public async Task<ModelsActionResult> AddCatalogueProviderAsync(CatalogueProviderRequest request, string apiKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(apiKey);
        var outcome = await AddCatalogueAsync(request, apiKey, request.PlanRuleAccepted, cancellationToken).ConfigureAwait(false);
        return outcome.IsSuccess ? ModelsActionResult.Success : ModelsActionResult.Failure(outcome.Message ?? ModelsCopy.ActionFailed);
    }

    /// <summary>Adds a catalogue listing to omp and says exactly what happened.</summary>
    /// <param name="request">The listing, route, placeholder values and model ids.</param>
    /// <param name="apiKey">The key, or an empty string when the route takes none or a key is already stored. It is passed on once and not kept.</param>
    /// <param name="planRuleAccepted">True when the user confirmed that they read the vendor's rule text. A coding plan listing is refused without it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome. It never holds the key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="apiKey"/> is null.</exception>
    internal async Task<CatalogueAddOutcome> AddCatalogueAsync(
        CatalogueProviderRequest request,
        string apiKey,
        bool planRuleAccepted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(apiKey);
        var row = CatalogueRows.Find(request.ListingId);
        if (row is null)
        {
            return CatalogueAddOutcome.Refused(CatalogueAddStatus.InvalidRequest, CatalogueAddCopy.UnknownListing);
        }

        if (row.Routes.Count == 0)
        {
            return CatalogueAddOutcome.Refused(CatalogueAddStatus.Unavailable, CatalogueAddCopy.SignInOnly(row.LoginCommand));
        }

        var route = row.Routes.FirstOrDefault(candidate => string.Equals(candidate.Id, request.RouteId, StringComparison.Ordinal));
        if (route is null)
        {
            return CatalogueAddOutcome.Refused(CatalogueAddStatus.InvalidRequest, CatalogueAddCopy.UnknownRoute);
        }

        if (row.Plan is not null && !planRuleAccepted)
        {
            return CatalogueAddOutcome.Refused(CatalogueAddStatus.PlanRuleNotAccepted, CatalogueAddCopy.PlanRuleNotAccepted);
        }

        var key = apiKey.Trim();
        var keyed = route.Auth != CatalogueAuthStyle.None;
        if (keyed && key.Length > 0 && CustomProviderRules.ValidateApiKey(key) is { } keyProblem)
        {
            return CatalogueAddOutcome.Refused(CatalogueAddStatus.InvalidRequest, keyProblem);
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return UsesOmpCredential(row, route)
                ? await ImportCredentialAsync(row, key, cancellationToken).ConfigureAwait(false)
                : await WriteBlockAsync(row, route, request, key, keyed, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    private static bool UsesOmpCredential(CatalogueRow row, CatalogueRoute route) =>
        row.IsOmpNative
        && row.UrlVariables.Count == 0
        && route.Auth != CatalogueAuthStyle.None
        && string.Equals(row.Routes[0].Id, route.Id, StringComparison.Ordinal);

    private async Task<CatalogueAddOutcome> ImportCredentialAsync(CatalogueRow row, string key, CancellationToken cancellationToken)
    {
        if (key.Length == 0)
        {
            return CatalogueAddOutcome.Refused(CatalogueAddStatus.KeyMissing, CatalogueAddCopy.KeyMissing);
        }

        var had = await _omp.Credentials.GetPresenceAsync(row.OmpProviderId!, cancellationToken).ConfigureAwait(false) == OmpCredentialPresence.Present;
        var imported = await _omp.Credentials.ImportApiKeyAsync(row.OmpProviderId!, new SecretString(key), cancellationToken).ConfigureAwait(false);
        if (!imported.IsImported)
        {
            return CatalogueAddOutcome.Refused(CatalogueAddStatus.OmpRejected, PlainText.WithoutPaths(imported.Message));
        }

        return new CatalogueAddOutcome(had ? CatalogueAddStatus.Updated : CatalogueAddStatus.Added);
    }

    private async Task<CatalogueAddOutcome> WriteBlockAsync(
        CatalogueRow row,
        CatalogueRoute route,
        CatalogueProviderRequest request,
        string key,
        bool keyed,
        CancellationToken cancellationToken)
    {
        var resolution = CatalogueUrlTemplate.Resolve(route.BaseAddressTemplate, request.UrlValues, allowLoopbackHttp: row.Region == CatalogueRegion.Local);
        if (resolution.Url is null)
        {
            return CatalogueAddOutcome.Refused(CatalogueAddStatus.InvalidUrlVariable, resolution.Problem ?? "The base URL is not valid.");
        }

        var blockId = row.BlockProviderId(route);
        var keyVariable = keyed ? ProviderKeyEnvironment.EnvNameFor(blockId) : null;
        if (keyVariable is not null && key.Length == 0)
        {
            await _keys.RefreshAsync(cancellationToken).ConfigureAwait(false);
            if (!_keys.Current.ContainsKey(keyVariable))
            {
                return CatalogueAddOutcome.Refused(CatalogueAddStatus.KeyMissing, CatalogueAddCopy.KeyMissing);
            }
        }

        var built = OmpCatalogueBlock.Build(row, route, request.UrlValues, keyVariable, request.ModelIds);
        if (built.Provider is null)
        {
            return CatalogueAddOutcome.Refused(CatalogueAddStatus.InvalidRequest, built.Problem ?? ModelsCopy.ActionFailed);
        }

        var managed = await _omp.ModelsFile.ReadManagedAsync(cancellationToken).ConfigureAwait(false);
        var existing = managed.Providers.FirstOrDefault(provider => string.Equals(provider.Id, blockId, StringComparison.Ordinal));
        if (keyVariable is not null && managed.Providers.Any(provider => !string.Equals(provider.Id, blockId, StringComparison.Ordinal)
            && string.Equals(provider.ApiKeyEnvName, keyVariable, StringComparison.Ordinal)))
        {
            return CatalogueAddOutcome.Refused(CatalogueAddStatus.OmpRejected, CatalogueAddCopy.KeyVariableTaken);
        }

        var write = await _omp.ModelsFile.UpsertAsync(built.Provider, cancellationToken).ConfigureAwait(false);
        if (!write.IsSatisfied)
        {
            return Refuse(write);
        }

        if (key.Length > 0 && keyVariable is not null)
        {
            try
            {
                await _secrets.SetAsync(keyVariable, new SecretString(key), cancellationToken).ConfigureAwait(false);
            }
            catch (SecretStoreException)
            {
                await RestoreBlockAsync(blockId, existing).ConfigureAwait(false);
                return CatalogueAddOutcome.Refused(CatalogueAddStatus.Unavailable, CatalogueAddCopy.KeyNotStored);
            }
        }

        await _keys.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return new CatalogueAddOutcome(existing is null ? CatalogueAddStatus.Added : CatalogueAddStatus.Updated);
    }

    private static CatalogueAddOutcome Refuse(OmpModelsWriteResult write) => write.Status switch
    {
        OmpModelsWriteStatus.Conflict => CatalogueAddOutcome.Refused(CatalogueAddStatus.OmpRejected, CatalogueAddCopy.NotOwned),
        OmpModelsWriteStatus.Failed => CatalogueAddOutcome.Refused(CatalogueAddStatus.Unavailable, DescribeOrDefault(write, CatalogueAddCopy.OmpUnavailable)),
        _ => CatalogueAddOutcome.Refused(CatalogueAddStatus.OmpRejected, DescribeOrDefault(write, ModelsCopy.ActionFailed)),
    };

    private static string DescribeOrDefault(OmpModelsWriteResult write, string fallback) =>
        string.IsNullOrWhiteSpace(write.Message) ? fallback : PlainText.WithoutPaths(write.Message);

    private async Task RestoreBlockAsync(string blockId, OmpManagedProvider? previous)
    {
        // The key could not be stored, so the block that names it must not stay behind half set up. A failed restore leaves a block
        // omp can read and the next add of the same id replaces it, so the failure is not a second error for the user.
        _ = previous is null
            ? await _omp.ModelsFile.RemoveAsync(blockId, CancellationToken.None).ConfigureAwait(false)
            : await _omp.ModelsFile.UpsertAsync(previous, CancellationToken.None).ConfigureAwait(false);
    }
}
