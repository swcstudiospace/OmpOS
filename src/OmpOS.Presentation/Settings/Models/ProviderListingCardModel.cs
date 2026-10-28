using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Settings.Models;

/// <summary>
/// One provider, aggregator, coding plan or local runtime on the Models page, with the setup form that adds it to omp: the protocol
/// route, the base URL placeholders, the model ids and a write-only key field. A coding plan card carries the vendor's rule text, the
/// tiers, the "interactive use only" tag and the sentence that cloud agents use pay-as-you-go keys. A listing whose credential is an
/// omp sign-in has no key field at all. The key lives only in <see cref="ApiKey"/>, goes to the source once and is cleared whether
/// the source succeeds or fails; it never appears on this model, in a notification, an error or an announcement.
/// </summary>
/// <remarks>
/// Threading: continuations run on the captured context, which is the UI context when a head starts them. A second save while one
/// runs does nothing, so a double click cannot send a key twice.
/// </remarks>
public sealed class ProviderListingCardModel : ObservableModel
{
    private const string Hidden = "[hidden]";

    private readonly IModelsSettingsSource _source;
    private readonly IProviderCardHost _host;
    private readonly Dictionary<string, ProviderListingUrlInput> _inputsByName = new(StringComparer.Ordinal);
    private ProviderListing _listing;
    private ProviderListingRoute? _selectedRoute;
    private IReadOnlyList<ProviderListingUrlInput> _urlInputs = [];
    private ApiKeyDraft? _apiKey;
    private string _modelIdsText = string.Empty;
    private bool _isSetupOpen;
    private bool _planRuleAccepted;
    private bool _isBusy;
    private string? _errorText;
    private string? _actionText;
    private string? _configuredText;

    /// <summary>Initializes a new instance of the <see cref="ProviderListingCardModel"/> class.</summary>
    /// <param name="listing">The listing.</param>
    /// <param name="source">The source every action goes through.</param>
    /// <param name="host">The page that owns the card.</param>
    internal ProviderListingCardModel(ProviderListing listing, IModelsSettingsSource source, IProviderCardHost host)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(host);
        _listing = listing;
        _source = source;
        _host = host;
        _selectedRoute = listing.Routes.Count > 0 ? listing.Routes[0] : null;
        RebuildInputs();
    }

    /// <summary>Gets the listing the card shows.</summary>
    public ProviderListing Listing => _listing;

    /// <summary>Gets the listing id.</summary>
    public string Id => _listing.Id;

    /// <summary>Gets the display name.</summary>
    public string DisplayName => _listing.DisplayName;

    /// <summary>Gets the region of the listing.</summary>
    public ProviderListingRegion Region => _listing.Region;

    /// <summary>Gets the kind of the listing.</summary>
    public ProviderListingKind Kind => _listing.Kind;

    /// <summary>Gets the heading of the region.</summary>
    public string RegionText => ProviderListingCopy.RegionTitle(_listing.Region);

    /// <summary>Gets the heading of the kind.</summary>
    public string KindText => ProviderListingCopy.KindTitle(_listing.Kind);

    /// <summary>Gets the country name, or null when the listing has none.</summary>
    public string? CountryText => _listing.Country is { } code ? ProviderListingCopy.CountryName(code) : null;

    /// <summary>Gets the line that says what the vendor states about residency, or null.</summary>
    public string? ResidencyText => _listing.ResidencyNote is { } note ? ProviderListingCopy.ResidencyText(note) : null;

    /// <summary>Gets the line that names the status and the date it was established.</summary>
    public string StatusText => ProviderListingCopy.StatusText(_listing.Status, _listing.StatusDate);

    /// <summary>Gets the mark of an unverified or partly verified listing, or null for a verified one.</summary>
    public string? VerifyMark => _listing.Status switch
    {
        ProviderListingStatus.Unverified => ProviderListingCopy.VerifyBeforeUse,
        ProviderListingStatus.Partial => ProviderListingCopy.PartlyVerified,
        _ => null,
    };

    /// <summary>Gets a value indicating whether the card must tell the user to verify the provider before use.</summary>
    public bool VerifyBeforeUse => _listing.Status == ProviderListingStatus.Unverified;

    /// <summary>Gets the short note of the listing, or null.</summary>
    public string? Note => _listing.Note;

    /// <summary>Gets the page the listing was read on.</summary>
    public Uri SourceUrl => _listing.SourceUrl;

    /// <summary>Gets the line that names the page the listing was read on.</summary>
    public string SourceText => ProviderListingCopy.SourceLine(_listing.SourceUrl);

    /// <summary>Gets the line that names the vendor documentation, or null.</summary>
    public string? DocsText => _listing.DocsUrl is { } url ? ProviderListingCopy.DocsLine(url) : null;

    /// <summary>Gets the line that names the page where a key is created, or null.</summary>
    public string? KeyPageText => _listing.KeyPageUrl is { } url ? ProviderListingCopy.KeyPageLine(url) : null;

    /// <summary>Gets the vendor documentation, or null.</summary>
    public Uri? DocsUrl => _listing.DocsUrl;

    /// <summary>Gets the page where a key is created, or null.</summary>
    public Uri? KeyPageUrl => _listing.KeyPageUrl;

    /// <summary>Gets the name of the environment variable that carries the key, or null. It is never a key.</summary>
    public string? KeyEnvironmentName => _listing.ApiKeyEnv;

    /// <summary>Gets a value indicating whether the listing's credential is an omp sign-in that OmpOS never stores.</summary>
    public bool IsSignInOnly => _listing.Routes.Count == 0 && _listing.LoginCommand is not null;

    /// <summary>Gets the sentence for a sign-in listing, or null.</summary>
    public string? SignInText => IsSignInOnly ? ProviderListingCopy.SignInText(_listing.LoginCommand!) : null;

    /// <summary>Gets a value indicating whether the listing is a coding plan with terms.</summary>
    public bool HasPlan => _listing.Plan is not null;

    /// <summary>Gets the vendor's rule for the plan, worded for the card, or null.</summary>
    public string? PlanRuleText => _listing.Plan is { } plan ? ProviderListingCopy.PlanRuleText(plan.RuleText) : null;

    /// <summary>Gets the tags of the plan: interactive use only, separate key, and that cloud agents use pay-as-you-go keys.</summary>
    public IReadOnlyList<string> PlanTags
    {
        get
        {
            if (_listing.Plan is not { } plan)
            {
                return [];
            }

            var tags = new List<string>();
            if (plan.InteractiveOnly)
            {
                tags.Add(ProviderListingCopy.InteractiveOnlyTag);
            }

            if (plan.SeparateKey)
            {
                tags.Add(ProviderListingCopy.SeparateKeyTag);
            }

            tags.Add(ProviderListingCopy.CloudAgentsSentence);
            return tags;
        }
    }

    /// <summary>Gets a value indicating whether the plan is limited to interactive use.</summary>
    public bool IsInteractiveOnly => _listing.Plan?.InteractiveOnly ?? false;

    /// <summary>Gets one line per plan tier, with the price (or "price unverified") and the published limits.</summary>
    public IReadOnlyList<string> PlanTierLines => _listing.Plan?.Tiers.Select(ProviderListingCopy.TierLine).ToArray() ?? [];

    /// <summary>Gets the page where the plan is described or bought, or null.</summary>
    public Uri? PlanUrl => _listing.Plan?.PlanUrl;

    /// <summary>Gets the page that states the plan's rule, or null.</summary>
    public Uri? PlanRuleSourceUrl => _listing.Plan?.RuleSourceUrl;

    /// <summary>Gets the sentence that says OmpOS sends a plan key only to the plan's own host, or null for a listing that is not a plan.</summary>
    public string? PlanHostText => HasPlan ? ProviderListingCopy.PlanHostSentence : null;

    /// <summary>Gets the routes of the listing, the first being the default.</summary>
    public IReadOnlyList<ProviderListingRoute> Routes => _listing.Routes;

    /// <summary>Gets a value indicating whether the listing has more than one route to choose from.</summary>
    public bool HasRouteChoice => _listing.Routes.Count > 1;

    /// <summary>Gets or sets the chosen route. It is null for a sign-in listing; a route that is not one of the listing's is ignored.</summary>
    public ProviderListingRoute? SelectedRoute
    {
        get => _selectedRoute;
        set
        {
            if (value is null || !_listing.Routes.Contains(value) || !SetProperty(ref _selectedRoute, value))
            {
                return;
            }

            RebuildInputs();
            NotifyRouteDerived();
        }
    }

    /// <summary>Gets the accessible name of the route choice.</summary>
    public string RouteFieldName => ProviderListingCopy.RouteFieldName(_listing.DisplayName);

    /// <summary>Gets the base URL of the chosen route, with the typed placeholder values filled in; placeholders still empty stay in braces.</summary>
    public string BaseAddressText => _selectedRoute is null
        ? string.Empty
        : ProviderListingUrl.Fill(_selectedRoute.BaseAddressTemplate, TypedValues());

    /// <summary>Gets the line that says how the chosen route sends the key.</summary>
    public string AuthText => _selectedRoute?.AuthText ?? string.Empty;

    /// <summary>Gets the placeholder inputs of the chosen route.</summary>
    public IReadOnlyList<ProviderListingUrlInput> UrlInputs => _urlInputs;

    /// <summary>Gets a value indicating whether the chosen route has placeholder inputs.</summary>
    public bool HasUrlInputs => _urlInputs.Count > 0;

    /// <summary>Gets a value indicating whether the form asks for model ids: omp keeps the models of a provider it knows, so only a provider OmpOS writes to <c>models.yml</c> takes them.</summary>
    public bool HasModelIdsField => !UsesOmpKey;

    /// <summary>Gets a value indicating whether the user has to name a model id before the provider can be added.</summary>
    public bool NeedsModelIds => !UsesOmpKey && (_selectedRoute?.NeedsUserModels ?? false);

    /// <summary>Gets the sentence under the model id field.</summary>
    public string ModelIdsHelp => NeedsModelIds ? ProviderListingCopy.ModelIdsNeededText : ProviderListingCopy.ModelIdsOptionalText;

    /// <summary>Gets the accessible name of the model id field.</summary>
    public string ModelIdsFieldName => ProviderListingCopy.ModelIdsFieldName(_listing.DisplayName);

    /// <summary>Gets or sets the model ids the user typed, separated by commas or spaces.</summary>
    public string ModelIdsText
    {
        get => _modelIdsText;
        set => SetProperty(ref _modelIdsText, value ?? string.Empty);
    }

    /// <summary>Gets a value indicating whether the chosen route takes a key from the user: the listing is not a sign-in listing and the route is not keyless.</summary>
    public bool TakesKey => _selectedRoute is { TakesKey: true };

    /// <summary>
    /// Gets a value indicating whether the key goes to omp itself: omp knows the provider by id, the listing has no placeholder and
    /// the default route is chosen, so no <c>models.yml</c> block is needed.
    /// </summary>
    public bool UsesOmpKey =>
        _listing.OmpProviderId is not null
        && _listing.Variables.Count == 0
        && _selectedRoute is not null
        && ReferenceEquals(_selectedRoute, _listing.Routes[0]);

    /// <summary>Gets a value indicating whether the listing can be set up from here: it is not a sign-in listing and, when omp knows it, takes a key.</summary>
    public bool CanSetUp => !IsSignInOnly && _selectedRoute is not null && (!UsesOmpKey || TakesKey);

    /// <summary>Gets a value indicating whether the Set up action is offered: the listing can be set up and its form is closed.</summary>
    public bool CanBeginSetup => CanSetUp && !_isSetupOpen;

    /// <summary>Gets the sentence that says where the key is kept, or null when the chosen route takes none.</summary>
    public string? KeyStorageNote => TakesKey ? ModelsCopy.KeyStorageNote(!UsesOmpKey) : null;

    /// <summary>Gets the open key entry, or null while the form is closed or the route takes no key.</summary>
    public ApiKeyDraft? ApiKey => _apiKey;

    /// <summary>Gets the sentence that says why the operator has disabled key entry, or null when keys can be entered.</summary>
    public string? KeyDisabledReason => TakesKey ? _host.KeyWritesDisabledReason : null;

    /// <summary>Gets a value indicating whether the operator has disabled key entry for this card: the key field is then disabled and adding is refused.</summary>
    public bool IsKeyDisabled => KeyDisabledReason is not null;

    /// <summary>Gets a value indicating whether the key field accepts input: the operator has not disabled key entry.</summary>
    public bool IsKeyEnabled => KeyDisabledReason is null;

    /// <summary>Gets a value indicating whether the form asks the user to tick that they read the vendor's rule text: the listing is a coding plan that takes a key.</summary>
    public bool RequiresPlanRule => HasPlan && TakesKey;

    /// <summary>Gets the label of the plan rule tick box.</summary>
    public string PlanRuleAcceptLabel => ProviderListingCopy.PlanRuleAcceptLabel;

    /// <summary>Gets the accessible name of the plan rule tick box.</summary>
    public string PlanRuleAcceptName => ProviderListingCopy.PlanRuleAcceptName(_listing.DisplayName);

    /// <summary>
    /// Gets or sets a value indicating whether the user ticked that they read the vendor's rule text. Adding a coding plan is refused until it is
    /// ticked; the tick goes to the source as <see cref="CatalogueProviderRequest.PlanRuleAccepted"/>. It starts unticked every time the form opens.
    /// </summary>
    public bool PlanRuleAccepted
    {
        get => _planRuleAccepted;
        set
        {
            if (SetProperty(ref _planRuleAccepted, value) && value)
            {
                ErrorText = null;
            }
        }
    }

    /// <summary>Gets a value indicating whether the setup form is open.</summary>
    public bool IsSetupOpen => _isSetupOpen;

    /// <summary>Gets the accessible name of the action that opens the form.</summary>
    public string SetupActionName => ProviderListingCopy.SetupActionName(_listing.DisplayName);

    /// <summary>Gets the accessible name of the action that adds the provider.</summary>
    public string AddActionName => ProviderListingCopy.AddActionName(_listing.DisplayName);

    /// <summary>Gets a value indicating whether an action is running.</summary>
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    /// <summary>Gets the problem the last action ran into, or null.</summary>
    public string? ErrorText { get => _errorText; private set => SetProperty(ref _errorText, value); }

    /// <summary>Gets the text of the last action that succeeded, or null.</summary>
    public string? ActionText { get => _actionText; private set => SetProperty(ref _actionText, value); }

    /// <summary>Gets the line that says the listing is already set up in omp, or null.</summary>
    public string? ConfiguredText => _configuredText;

    /// <summary>Opens the setup form. A listing that cannot be set up here ignores the request.</summary>
    public void BeginSetup()
    {
        if (_isSetupOpen || !CanSetUp)
        {
            return;
        }

        ErrorText = null;
        ActionText = null;
        _isSetupOpen = true;
        _planRuleAccepted = false;
        NotifyPropertyChanged(nameof(PlanRuleAccepted));
        NotifyPropertyChanged(nameof(IsSetupOpen));
        NotifyPropertyChanged(nameof(CanBeginSetup));
        ResetKeyDraft();
    }

    /// <summary>Closes the setup form and clears the typed key.</summary>
    public void CancelSetup()
    {
        if (!_isSetupOpen)
        {
            return;
        }

        _apiKey?.Clear();
        _isSetupOpen = false;
        _planRuleAccepted = false;
        NotifyPropertyChanged(nameof(PlanRuleAccepted));
        SetApiKey(null);
        ErrorText = null;
        NotifyPropertyChanged(nameof(IsSetupOpen));
        NotifyPropertyChanged(nameof(CanBeginSetup));
    }

    /// <summary>
    /// Checks the form and hands the provider to the source once: the key alone to omp for a provider omp knows, or the listing, the
    /// route, the placeholder values and the model ids for a provider OmpOS writes to <c>models.yml</c>. The typed key is cleared
    /// whether the source succeeds or fails. On success the form closes and the page reads the list again.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when omp now has the provider.</returns>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!_isSetupOpen || _isBusy || _selectedRoute is null)
        {
            return false;
        }

        if (KeyDisabledReason is { } disabledReason)
        {
            _apiKey?.Clear();
            ErrorText = disabledReason;
            return false;
        }

        var problem = FormProblem();
        if (problem is not null)
        {
            ErrorText = problem;
            return false;
        }

        var draft = _apiKey;
        var key = draft?.TakeKey() ?? string.Empty;
        var request = new CatalogueProviderRequest(
            _listing.Id,
            _selectedRoute.Id,
            TypedValues(),
            ParseModelIds(_modelIdsText),
            _planRuleAccepted);
        var result = await SendAsync(request, key, draft, cancellationToken).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            ErrorText = Hide(result.Message, key) ?? ModelsCopy.ActionFailed;
            return false;
        }

        _isSetupOpen = false;
        SetApiKey(null);
        NotifyPropertyChanged(nameof(IsSetupOpen));
        NotifyPropertyChanged(nameof(CanBeginSetup));
        var added = ProviderListingCopy.Added(_listing.DisplayName);
        ActionText = added;
        await _host.ChangedAsync(added, cancellationToken).ConfigureAwait(true);
        return true;
    }

    private async Task<ModelsActionResult> SendAsync(CatalogueProviderRequest request, string key, ApiKeyDraft? draft, CancellationToken cancellationToken)
    {
        ErrorText = null;
        ActionText = null;
        IsBusy = true;
        if (draft is not null)
        {
            draft.IsSaving = true;
        }

        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            return UsesOmpKey
                ? await _source.SaveApiKeyAsync(_listing.OmpProviderId!, key, cancellationToken).ConfigureAwait(true)
                : await _source.AddCatalogueProviderAsync(request, key, cancellationToken).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            return ModelsActionResult.Failure(exception.Message);
        }
        finally
        {
            draft?.Clear();
            if (draft is not null)
            {
                draft.IsSaving = false;
            }

            IsBusy = false;
        }
    }

    /// <summary>Replaces the listing with a newer read of it, keeping what the user typed and the chosen route where they still apply.</summary>
    /// <param name="listing">The listing.</param>
    internal void Update(ProviderListing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        var routeId = _selectedRoute?.Id;
        _listing = listing;
        _selectedRoute = listing.Routes.FirstOrDefault(route => string.Equals(route.Id, routeId, StringComparison.Ordinal))
            ?? (listing.Routes.Count > 0 ? listing.Routes[0] : null);
        RebuildInputs();
        NotifyPropertyChanged(string.Empty);
    }

    /// <summary>Announces that the operator's key entry switch may have changed.</summary>
    internal void RefreshKeyDisabled()
    {
        NotifyPropertyChanged(nameof(KeyDisabledReason));
        NotifyPropertyChanged(nameof(IsKeyDisabled));
        NotifyPropertyChanged(nameof(IsKeyEnabled));
    }

    /// <summary>Sets the line that says the listing is already set up in omp.</summary>
    /// <param name="text">The line, or null.</param>
    internal void SetConfigured(string? text)
    {
        if (!string.Equals(_configuredText, text, StringComparison.Ordinal))
        {
            _configuredText = text;
            NotifyPropertyChanged(nameof(ConfiguredText));
        }
    }

    /// <summary>Gets the id the listing has in omp once set up: the omp provider id, or the id of its <c>models.yml</c> block.</summary>
    public string ConfiguredId =>
        UsesOmpKey
            ? _listing.OmpProviderId!
            : _selectedRoute is null || ReferenceEquals(_selectedRoute, _listing.Routes[0]) ? _listing.Id : $"{_listing.Id}-{_selectedRoute.Id}";

    private static List<string> ParseModelIds(string text)
    {
        var ids = new List<string>();
        foreach (var part in text.Split([',', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ids.Contains(part, StringComparer.Ordinal))
            {
                ids.Add(part);
            }
        }

        return ids;
    }

    private static string? Hide(string? message, string key)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return key.Length == 0 ? message : message.Replace(key, Hidden, StringComparison.Ordinal);
    }

    private string? FormProblem()
    {
        string? first = null;
        foreach (var input in _urlInputs)
        {
            if (!input.Validate())
            {
                first ??= input.Error;
            }
        }

        if (first is not null)
        {
            return first;
        }

        if (NeedsModelIds && ParseModelIds(_modelIdsText).Count == 0)
        {
            return ProviderListingCopy.ModelIdsNeededText;
        }

        if (TakesKey)
        {
            var draft = _apiKey;
            if (draft is null || !draft.IsValid)
            {
                return draft?.Error ?? ModelsCopy.ErrApiKeyRequired;
            }
        }

        if (RequiresPlanRule && !_planRuleAccepted)
        {
            return ProviderListingCopy.PlanRuleRequired;
        }

        return null;
    }

    private Dictionary<string, string> TypedValues()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var input in _inputsByName.Values)
        {
            var trimmed = input.Value.Trim();
            if (ProviderListingUrl.IsAcceptable(trimmed))
            {
                values[input.Name] = trimmed;
            }
        }

        return values;
    }

    private void RebuildInputs()
    {
        foreach (var variable in _listing.Variables)
        {
            if (!_inputsByName.TryGetValue(variable.Name, out var input) || !ReferenceEquals(input.Variable, variable))
            {
                var typed = input?.Value ?? string.Empty;
                input = new ProviderListingUrlInput(variable, _listing.DisplayName) { Value = typed };
                _inputsByName[variable.Name] = input;
                input.PropertyChanged += (_, args) =>
                {
                    if (string.Equals(args.PropertyName, nameof(ProviderListingUrlInput.Value), StringComparison.Ordinal))
                    {
                        NotifyPropertyChanged(nameof(BaseAddressText));
                    }
                };
            }
        }

        var used = _selectedRoute is null ? [] : ProviderListingUrl.VariablesIn(_selectedRoute.BaseAddressTemplate);
        _urlInputs = [.. used.Where(_inputsByName.ContainsKey).Select(name => _inputsByName[name])];
    }

    private void ResetKeyDraft()
    {
        SetApiKey(TakesKey ? new ApiKeyDraft(_listing.Id, _listing.DisplayName, !UsesOmpKey) : null);
    }

    private void SetApiKey(ApiKeyDraft? draft)
    {
        if (!ReferenceEquals(_apiKey, draft))
        {
            _apiKey = draft;
            NotifyPropertyChanged(nameof(ApiKey));
        }
    }

    private void NotifyRouteDerived()
    {
        NotifyPropertyChanged(nameof(UrlInputs));
        NotifyPropertyChanged(nameof(HasUrlInputs));
        NotifyPropertyChanged(nameof(BaseAddressText));
        NotifyPropertyChanged(nameof(AuthText));
        NotifyPropertyChanged(nameof(NeedsModelIds));
        NotifyPropertyChanged(nameof(HasModelIdsField));
        NotifyPropertyChanged(nameof(ModelIdsHelp));
        NotifyPropertyChanged(nameof(TakesKey));
        NotifyPropertyChanged(nameof(UsesOmpKey));
        NotifyPropertyChanged(nameof(CanSetUp));
        NotifyPropertyChanged(nameof(CanBeginSetup));
        NotifyPropertyChanged(nameof(KeyStorageNote));
        if (_isSetupOpen)
        {
            _apiKey?.Clear();
            ResetKeyDraft();
        }
    }
}
