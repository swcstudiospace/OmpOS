using System.Collections.Concurrent;
using System.Text.Json;
using Autonogrammer.Harness.OmpConfig;
using Autonogrammer.Harness.Providers;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Models;

namespace Autonogrammer.Native.Settings.Models;

/// <summary>
/// The Models page's access to omp, backed by omp's own mechanisms so that every action changes what omp uses: the model listing
/// (<c>omp models --json</c>), credential presence (<c>omp token</c>), a key import (<c>omp auth import -</c>), the default model
/// (<c>omp config set modelRoles</c>) and the Autonogrammer blocks of <c>models.yml</c>. Nothing here validates a credential: a key is
/// reported as present, never as working, and a test only asks whether something answers at an address.
/// </summary>
/// <remarks>
/// <para>
/// omp resolves a stored credential only for the providers it knows, so a key for a built-in provider goes to omp's credential
/// database through <c>omp auth import</c> and Autonogrammer keeps no copy. A custom provider is different: its block in <c>models.yml</c> names
/// an environment variable that omp reads from its own process environment, so that key is kept in the Autonogrammer provider key store (plain
/// text, owner-only file; see <see cref="OwnerOnlyFileSecretStore"/>) and <see cref="ProviderKeyEnvironment"/> hands it to every omp the
/// head starts. Removing the provider deletes the key.
/// </para>
/// <para>
/// Writes are serialised, one at a time. A <c>models.yml</c> change applies to omp processes started afterwards; the registry entry that
/// keeps a provider's display name and routes is written first and restored when omp refuses the block.
/// </para>
/// </remarks>
internal sealed partial class ModelsSettingsSource : IModelsSettingsSource, IDisposable
{
    private const int TokenParallelism = 6;

    private readonly OmpModelConfiguration _omp;
    private readonly ISecretStore _secrets;
    private readonly ProviderKeyEnvironment _keys;
    private readonly ProviderRegistryStore _details;
    private readonly LocalProviderProbe _probe;
    private readonly ProviderErrorMapper _errors;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    /// <summary>Creates the source.</summary>
    /// <param name="omp">omp's model configuration: the listing, the credential store, the default model and <c>models.yml</c>.</param>
    /// <param name="secrets">The Autonogrammer provider key store, which holds the key of each keyed custom provider under its variable name.</param>
    /// <param name="keys">The environment the keys of that store are merged into; it is refreshed after every change of a key.</param>
    /// <param name="details">The registry file that keeps the display name and routes of each custom provider.</param>
    /// <param name="probe">The probe that asks a local endpoint for its model list.</param>
    /// <param name="errors">The mapper that words an HTTP status of a probe.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ModelsSettingsSource(
        OmpModelConfiguration omp,
        ISecretStore secrets,
        ProviderKeyEnvironment keys,
        ProviderRegistryStore details,
        LocalProviderProbe probe,
        ProviderErrorMapper errors)
    {
        ArgumentNullException.ThrowIfNull(omp);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(errors);
        _omp = omp;
        _secrets = secrets;
        _keys = keys;
        _details = details;
        _probe = probe;
        _errors = errors;
    }

    /// <inheritdoc />
    public void Dispose() => _writeGate.Dispose();

    /// <inheritdoc />
    public async Task<ModelsSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var listing = await _omp.Catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var managed = await _omp.ModelsFile.ReadManagedAsync(cancellationToken).ConfigureAwait(false);
        var roles = await _omp.DefaultModel.GetAsync(cancellationToken).ConfigureAwait(false);
        var (details, detailsProblem) = await ReadDetailsAsync(cancellationToken).ConfigureAwait(false);
        await _keys.RefreshAsync(cancellationToken).ConfigureAwait(false);

        var modelsByProvider = GroupModels(listing, out var listedOrder);
        var managedIds = managed.Providers.Select(static provider => provider.Id).ToHashSet(StringComparer.Ordinal);
        var presence = await ReadPresenceAsync(listing, listedOrder, managedIds, cancellationToken).ConfigureAwait(false);

        var providers = BuildProviders(managed, details, modelsByProvider, listedOrder, managedIds, presence);

        if (!listing.IsComplete && listing.Models.Count == 0 && managed.Providers.Count == 0
            && !presence.IsEmpty && presence.Values.All(static value => value == OmpCredentialPresence.Unknown))
        {
            throw new SettingsSourceException(ModelsCopy.LoadFailed);
        }

        var warnings = new List<string>();
        AddWarning(warnings, listing.Warning);
        foreach (var problem in managed.Problems)
        {
            AddWarning(warnings, problem);
        }

        AddWarning(warnings, roles.Problem is null ? null : "The default model could not be read.");
        AddWarning(warnings, detailsProblem);
        AddWarning(warnings, _keys.Problem);
        return new ModelsSnapshot(
            providers,
            roles.DefaultSelector,
            warnings.Count == 0 ? null : string.Join(' ', warnings),
            listing.IsComplete,
            ProviderTemplateMapper.FromCatalogue(),
            CatalogueListingMapper.FromCatalogue());
    }

    /// <inheritdoc />
    public async Task<ModelsActionResult> SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(apiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ModelsActionResult.Failure(ModelsCopy.ErrApiKeyRequired);
        }

        if (CustomProviderRules.ValidateApiKey(apiKey) is { } keyProblem)
        {
            return ModelsActionResult.Failure(keyProblem);
        }

        if (!OmpProviderId.IsValid(providerId))
        {
            return ModelsActionResult.Failure("This provider id is not one omp accepts.");
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var managed = await _omp.ModelsFile.ReadManagedAsync(cancellationToken).ConfigureAwait(false);
            var block = managed.Providers.FirstOrDefault(provider => string.Equals(provider.Id, providerId, StringComparison.Ordinal));
            if (block is null)
            {
                var imported = await _omp.Credentials.ImportApiKeyAsync(providerId, new SecretString(apiKey), cancellationToken).ConfigureAwait(false);
                return imported.IsImported ? ModelsActionResult.Success : ModelsActionResult.Failure(PlainText.WithoutPaths(imported.Message));
            }

            if (block.ApiKeyEnvName is null)
            {
                return ModelsActionResult.Failure("This provider is set to need no key, so there is nothing to store. Edit the provider to use a key.");
            }

            try
            {
                await _secrets.SetAsync(block.ApiKeyEnvName, new SecretString(apiKey), cancellationToken).ConfigureAwait(false);
            }
            catch (SecretStoreException exception)
            {
                return ModelsActionResult.Failure("Autonogrammer could not store the key. " + exception.Message);
            }

            await _keys.RefreshAsync(cancellationToken).ConfigureAwait(false);
            return ModelsActionResult.Success;
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ModelsActionResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selector);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _omp.DefaultModel.SetDefaultAsync(selector, cancellationToken).ConfigureAwait(false);
            return result.IsSet ? ModelsActionResult.Success : ModelsActionResult.Failure(PlainText.WithoutPaths(result.Message));
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ModelsActionResult> SaveCustomProviderAsync(CustomProviderForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var definition = CustomProviderMapper.ToDefinition(form);
        if (CustomProviderMapper.FirstProblem(definition) is { } problem)
        {
            return ModelsActionResult.Failure(problem);
        }

        if (OmpBuiltInProviders.Find(form.Id) is not null || OmpBuiltInProviders.FindLocalEngine(form.Id) is not null)
        {
            return ModelsActionResult.Failure("omp already has a provider with this id of its own. Choose another id.");
        }

        var keyVariable = ProviderKeyEnvironment.EnvNameFor(form.Id);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await SaveCustomProviderLockedAsync(form, definition, keyVariable, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    private async Task<ModelsActionResult> SaveCustomProviderLockedAsync(
        CustomProviderForm form,
        CustomProviderDefinition definition,
        string keyVariable,
        CancellationToken cancellationToken)
    {
        var managed = await _omp.ModelsFile.ReadManagedAsync(cancellationToken).ConfigureAwait(false);
        if (managed.Providers.Any(provider => !string.Equals(provider.Id, form.Id, StringComparison.Ordinal)
            && string.Equals(provider.ApiKeyEnvName, keyVariable, StringComparison.Ordinal)))
        {
            return ModelsActionResult.Failure("Another provider already uses the key variable this id would get. Choose an id that differs by more than punctuation.");
        }

        var (previous, detailsProblem) = await ReadDetailsAsync(cancellationToken).ConfigureAwait(false);
        if (detailsProblem is not null)
        {
            return ModelsActionResult.Failure(detailsProblem + " Fix or remove that file, then save again.");
        }

        var next = Replace(previous, definition);
        try
        {
            await _details.WriteAsync(next, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ModelsActionResult.Failure("Autonogrammer could not save the provider's details (" + exception.GetType().Name + "). Nothing was changed.");
        }

        OmpModelsWriteResult write;
        try
        {
            write = await _omp.ModelsFile.UpsertAsync(CustomProviderMapper.ToManaged(form, keyVariable), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await RestoreDetailsAsync(previous).ConfigureAwait(false);
            throw;
        }

        if (!write.IsSatisfied)
        {
            await RestoreDetailsAsync(previous).ConfigureAwait(false);
            return ModelsActionResult.Failure(Describe(write));
        }

        var hadKey = managed.Providers.Any(provider => string.Equals(provider.Id, form.Id, StringComparison.Ordinal) && provider.ApiKeyEnvName is not null);
        if (hadKey && form.Auth == CustomProviderAuth.None)
        {
            // A provider that no longer uses a key must not keep one.
            var forgotten = await ForgetKeyAsync(keyVariable, cancellationToken).ConfigureAwait(false);
            if (forgotten is not null)
            {
                return forgotten;
            }
        }

        await _keys.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return ModelsActionResult.Success;
    }

    /// <inheritdoc />
    public async Task<ModelsActionResult> RemoveCustomProviderAsync(string providerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        if (!OmpProviderId.IsValid(providerId))
        {
            return ModelsActionResult.Failure("This provider id is not one omp accepts.");
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var managed = await _omp.ModelsFile.ReadManagedAsync(cancellationToken).ConfigureAwait(false);
            var block = managed.Providers.FirstOrDefault(provider => string.Equals(provider.Id, providerId, StringComparison.Ordinal));
            if (block is null)
            {
                return ModelsActionResult.Failure(managed.Problems.Count > 0
                    ? PlainText.WithoutPaths(managed.Problems[0])
                    : "Autonogrammer did not write a provider with this id, so it does not remove it.");
            }

            var write = await _omp.ModelsFile.RemoveAsync(providerId, cancellationToken).ConfigureAwait(false);
            if (!write.IsSatisfied)
            {
                return ModelsActionResult.Failure(Describe(write));
            }

            var forgotten = await ForgetKeyAsync(block.ApiKeyEnvName ?? ProviderKeyEnvironment.EnvNameFor(providerId), cancellationToken).ConfigureAwait(false);
            await PruneDetailsAsync(providerId, cancellationToken).ConfigureAwait(false);
            await _keys.RefreshAsync(cancellationToken).ConfigureAwait(false);
            return forgotten ?? ModelsActionResult.Success;
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ProviderTestOutcome> TestAsync(string providerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        var managed = await _omp.ModelsFile.ReadManagedAsync(cancellationToken).ConfigureAwait(false);
        var block = managed.Providers.FirstOrDefault(provider => string.Equals(provider.Id, providerId, StringComparison.Ordinal));
        if (block is not null)
        {
            var testable = block.Api != OmpProviderApi.AnthropicMessages
                && (string.Equals(block.BaseUrl.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || block.BaseUrl.IsLoopback);
            return testable
                ? await ProbeAsync(block.Id, block.BaseUrl, block.NoAuthentication ? ProviderAuthType.None : ProviderAuthType.ApiKey, cancellationToken).ConfigureAwait(false)
                : NotTested;
        }

        if (OmpBuiltInProviders.FindLocalEngine(providerId) is { } engine)
        {
            return await ProbeAsync(engine.Id, engine.Endpoint, ProviderAuthType.None, cancellationToken).ConfigureAwait(false);
        }

        return NotTested;
    }

    private static readonly ProviderTestOutcome NotTested = new(ProviderTestStatus.NotTestable, string.Empty);

    private List<ProviderInfo> BuildProviders(
        OmpManagedReadResult managed,
        IReadOnlyList<CustomProviderDefinition> details,
        Dictionary<string, List<string>> modelsByProvider,
        List<string> listedOrder,
        HashSet<string> managedIds,
        ConcurrentDictionary<string, OmpCredentialPresence> presence)
    {
        var providers = new List<ProviderInfo>();
        foreach (var engine in OmpBuiltInProviders.LocalEngines)
        {
            if (modelsByProvider.TryGetValue(engine.Id, out var engineModels) && !managedIds.Contains(engine.Id))
            {
                providers.Add(LocalCard(engine, engineModels));
            }
        }

        foreach (var builtIn in OmpBuiltInProviders.All)
        {
            if (!managedIds.Contains(builtIn.Id))
            {
                providers.Add(BuiltInCard(builtIn, modelsByProvider.GetValueOrDefault(builtIn.Id) ?? [], presence));
            }
        }

        foreach (var id in listedOrder)
        {
            if (!managedIds.Contains(id) && OmpBuiltInProviders.Find(id) is null && OmpBuiltInProviders.FindLocalEngine(id) is null)
            {
                providers.Add(UnknownCard(id, modelsByProvider[id], presence));
            }
        }

        foreach (var block in managed.Providers)
        {
            providers.Add(CustomCard(
                block,
                details.FirstOrDefault(entry => string.Equals(entry.Id, block.Id, StringComparison.Ordinal)),
                modelsByProvider.GetValueOrDefault(block.Id),
                KeyState(block)));
        }

        return providers;
    }

    private static ProviderInfo LocalCard(OmpLocalEngine engine, IReadOnlyList<string> models) =>
        new(
            engine.Id,
            engine.DisplayName,
            ProviderGroup.Local,
            engine.Endpoint.OriginalString,
            ProviderAuthKind.None,
            CredentialState.NotRequired,
            models,
            SourceUrl: null,
            RetrievedOn: null,
            Plan: null,
            IsAutonogrammerManaged: false,
            "omp finds this engine on its default address unless an environment variable moves it. The connection test checks that address.");

    private static ProviderInfo BuiltInCard(OmpBuiltInProvider provider, IReadOnlyList<string> models, ConcurrentDictionary<string, OmpCredentialPresence> presence) =>
        new(
            provider.Id,
            provider.DisplayName,
            ProviderGroup.OmpBuiltIn,
            string.Empty,
            ProviderAuthKind.ApiKey,
            StateOf(presence, provider.Id),
            models,
            provider.KeyPage,
            provider.ReadOn,
            Plan: null,
            IsAutonogrammerManaged: false,
            Note: null);

    private static ProviderInfo UnknownCard(string id, IReadOnlyList<string> models, ConcurrentDictionary<string, OmpCredentialPresence> presence) =>
        new(
            id,
            id,
            ProviderGroup.OmpBuiltIn,
            string.Empty,
            ProviderAuthKind.Unknown,
            StateOf(presence, id),
            models,
            SourceUrl: null,
            RetrievedOn: null,
            Plan: null,
            IsAutonogrammerManaged: false,
            Note: null);

    private static CredentialState ToState(OmpCredentialPresence presence) => presence switch
    {
        OmpCredentialPresence.Present => CredentialState.Present,
        OmpCredentialPresence.Absent => CredentialState.Absent,
        _ => CredentialState.Unknown,
    };

    private static CredentialState StateOf(ConcurrentDictionary<string, OmpCredentialPresence> presence, string id) =>
        ToState(presence.TryGetValue(id, out var known) ? known : OmpCredentialPresence.Unknown);

    private CredentialState KeyState(OmpManagedProvider block)
    {
        if (block.ApiKeyEnvName is null)
        {
            return CredentialState.NotRequired;
        }

        if (_keys.Problem is not null)
        {
            return CredentialState.Unknown;
        }

        return _keys.Current.ContainsKey(block.ApiKeyEnvName) ? CredentialState.Present : CredentialState.Absent;
    }

    private static void AddWarning(List<string> warnings, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            warnings.Add(PlainText.WithoutPaths(text));
        }
    }

    private static string Describe(OmpModelsWriteResult write) => write.Status switch
    {
        OmpModelsWriteStatus.Conflict => "A provider with this id already exists in models.yml that Autonogrammer does not own. Choose another id, or remove that provider from models.yml by hand.",
        _ => string.IsNullOrWhiteSpace(write.Message) ? ModelsCopy.ActionFailed : PlainText.WithoutPaths(write.Message),
    };

    private static Dictionary<string, List<string>> GroupModels(OmpModelListing listing, out List<string> order)
    {
        var byProvider = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        order = [];
        foreach (var model in listing.Models)
        {
            if (!byProvider.TryGetValue(model.ProviderId, out var selectors))
            {
                selectors = [];
                byProvider[model.ProviderId] = selectors;
                order.Add(model.ProviderId);
            }

            selectors.Add(model.Selector);
        }

        return byProvider;
    }

    private static List<CustomProviderDefinition> Replace(IReadOnlyList<CustomProviderDefinition> existing, CustomProviderDefinition definition)
    {
        var next = new List<CustomProviderDefinition>(existing.Count + 1);
        var replaced = false;
        foreach (var entry in existing)
        {
            if (string.Equals(entry.Id, definition.Id, StringComparison.Ordinal))
            {
                next.Add(definition);
                replaced = true;
            }
            else
            {
                next.Add(entry);
            }
        }

        if (!replaced)
        {
            next.Add(definition);
        }

        return next;
    }

    private static ProviderInfo CustomCard(OmpManagedProvider block, CustomProviderDefinition? details, List<string>? listed, CredentialState credential)
    {
        var form = CustomProviderMapper.ToForm(block, details);
        return new ProviderInfo(
            block.Id,
            form.DisplayName,
            ProviderGroup.Custom,
            block.BaseUrl.OriginalString,
            block.NoAuthentication ? ProviderAuthKind.None : ProviderAuthKind.ApiKey,
            credential,
            listed is { Count: > 0 } ? listed : [.. block.Models.Select(model => block.Id + "/" + model.Id)],
            SourceUrl: null,
            RetrievedOn: null,
            Plan: null,
            IsAutonogrammerManaged: true,
            block.NoAuthentication
                ? null
                : "Autonogrammer keeps this provider's key as plain text in a file only your account can read, and gives it to every omp it starts. Removing the provider deletes the key.",
            form);
    }

    private async Task<ConcurrentDictionary<string, OmpCredentialPresence>> ReadPresenceAsync(
        OmpModelListing listing,
        List<string> listedOrder,
        HashSet<string> managedIds,
        CancellationToken cancellationToken)
    {
        var presence = new ConcurrentDictionary<string, OmpCredentialPresence>(StringComparer.Ordinal);
        var ask = new List<string>();
        var shown = OmpBuiltInProviders.All.Select(static provider => provider.Id)
            .Concat(listedOrder.Where(static id => OmpBuiltInProviders.FindLocalEngine(id) is null))
            .Where(id => !managedIds.Contains(id))
            .Distinct(StringComparer.Ordinal);
        foreach (var id in shown)
        {
            // omp lists only the models of providers it holds a credential for, so a provider in a complete listing needs no question.
            if (listing.IsComplete && listedOrder.Contains(id, StringComparer.Ordinal))
            {
                presence[id] = OmpCredentialPresence.Present;
            }
            else
            {
                ask.Add(id);
            }
        }

        var options = new ParallelOptions { MaxDegreeOfParallelism = TokenParallelism, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(
            ask,
            options,
            async (id, token) => presence[id] = await _omp.Credentials.GetPresenceAsync(id, token).ConfigureAwait(false)).ConfigureAwait(false);
        return presence;
    }

    private async Task<(IReadOnlyList<CustomProviderDefinition> Details, string? Problem)> ReadDetailsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await _details.ReadAsync(cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return ([], "Autonogrammer's saved details of custom providers could not be read, so names and routes are taken from models.yml.");
        }
    }

    private async Task RestoreDetailsAsync(IReadOnlyList<CustomProviderDefinition> previous)
    {
        try
        {
            await _details.WriteAsync(previous, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The block is the truth omp reads; an entry that could not be restored only holds a name and routes, and the next save replaces it.
        }
    }

    private async Task PruneDetailsAsync(string providerId, CancellationToken cancellationToken)
    {
        var (existing, problem) = await ReadDetailsAsync(cancellationToken).ConfigureAwait(false);
        if (problem is not null || !existing.Any(entry => string.Equals(entry.Id, providerId, StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            await _details.WriteAsync([.. existing.Where(entry => !string.Equals(entry.Id, providerId, StringComparison.Ordinal))], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The provider is gone from models.yml, which is what omp reads; a leftover entry is ignored and replaced by the next save of this id.
        }
    }

    private async Task<ModelsActionResult?> ForgetKeyAsync(string keyVariable, CancellationToken cancellationToken)
    {
        try
        {
            _ = await _secrets.RemoveAsync(keyVariable, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (SecretStoreException exception)
        {
            return ModelsActionResult.Failure("The provider was changed in omp, but Autonogrammer could not delete its stored key. " + exception.Message);
        }
    }

    private async Task<ProviderTestOutcome> ProbeAsync(string id, Uri endpoint, ProviderAuthType auth, CancellationToken cancellationToken)
    {
        var entry = new ProviderEntry(
            id,
            ProviderKind.Local,
            ProviderTransport.OpenAiCompatible,
            auth,
            endpoint,
            ProviderCapabilities.ChatCompletions,
            SourceUrl: null,
            RetrievedOn: default);
        var result = await _probe.ProbeAsync(entry, cancellationToken).ConfigureAwait(false);
        return result.Status switch
        {
            ProviderProbeStatus.Running => new ProviderTestOutcome(ProviderTestStatus.Reachable, result.Message),
            ProviderProbeStatus.RunningAuthRequired => new ProviderTestOutcome(ProviderTestStatus.Unauthorized, result.Message),
            ProviderProbeStatus.NotRunning => new ProviderTestOutcome(ProviderTestStatus.NotRunning, result.Message),
            _ => new ProviderTestOutcome(ProviderTestStatus.Failed, WordUnexpected(entry, result)),
        };
    }

    private string WordUnexpected(ProviderEntry entry, ProviderProbeResult result) =>
        result.HttpStatus is { } status and (402 or 429 or >= 500)
            ? _errors.Map(entry, new ProviderFailure(status)).Message
            : result.Message;
}
