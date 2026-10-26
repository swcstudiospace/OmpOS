using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Settings.Models;

/// <summary>
/// One provider card on the Models page: what the provider is, whether omp holds a credential, its models, and the actions the
/// source can back. Actions that omp cannot do from here have no member: there is no sign-in action (omp signs in by itself) and
/// edit and remove exist only for providers OmpOS wrote. Action failures set <see cref="ErrorText"/> and never throw.
/// </summary>
/// <remarks>
/// Threading: continuations run on the captured context, which is the UI context when a head starts them. An action that is
/// already running makes the same action do nothing, so a double click cannot send a key twice.
/// </remarks>
public sealed class ProviderCardModel : ObservableModel
{
    private const string Hidden = "[hidden]";

    private readonly IModelsSettingsSource _source;
    private readonly IProviderCardHost _host;
    private ProviderInfo _info;
    private string? _defaultModel;
    private IReadOnlyList<ProviderModelRow> _models = [];
    private ApiKeyDraft? _apiKey;
    private bool _isConfirmingRemove;
    private bool _isTesting;
    private bool _isBusy;
    private string? _testText;
    private ProviderTestStatus? _testStatus;
    private string? _errorText;
    private string? _actionText;

    /// <summary>Initializes a new instance of the <see cref="ProviderCardModel"/> class.</summary>
    /// <param name="info">The provider.</param>
    /// <param name="defaultModel">The current default model selector, or null.</param>
    /// <param name="source">The source every action goes through.</param>
    /// <param name="host">The page that owns the card.</param>
    internal ProviderCardModel(ProviderInfo info, string? defaultModel, IModelsSettingsSource source, IProviderCardHost host)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(host);
        _info = info;
        _defaultModel = defaultModel;
        _source = source;
        _host = host;
        _models = BuildRows();
    }

    /// <summary>Gets the provider the card shows.</summary>
    public ProviderInfo Info => _info;

    /// <summary>Gets the provider id.</summary>
    public string Id => _info.Id;

    /// <summary>Gets the display name.</summary>
    public string DisplayName => _info.DisplayName;

    /// <summary>Gets the group the card sits in.</summary>
    public ProviderGroup Group => _info.Group;

    /// <summary>Gets the endpoint as text, or an empty string when omp owns it.</summary>
    public string EndpointText => _info.EndpointText;

    /// <summary>Gets the line that says how the provider authenticates and whether omp holds a credential.</summary>
    public string StatusLine => ModelsCopy.StatusLine(_info.AuthKind, _info.Credential);

    /// <summary>Gets the short note of the card, or null.</summary>
    public string? Note => _info.Note;

    /// <summary>Gets the hint for a provider that signs in through omp, or null for every other provider.</summary>
    public string? SignInHint => _info.AuthKind == ProviderAuthKind.OAuth ? ModelsCopy.SignInHint : null;

    /// <summary>Gets the models the provider offers.</summary>
    public IReadOnlyList<ProviderModelRow> Models => _models;

    /// <summary>Gets the line that names the page a catalogue row was verified on and the date it was read, or null for other rows.</summary>
    public string? SourceText => _info.SourceUrl is { } url && _info.RetrievedOn is { } date ? ModelsCopy.SourceLine(url, date) : null;

    /// <summary>Gets the page a catalogue row was verified on, or null.</summary>
    public Uri? SourceUrl => _info.SourceUrl;

    /// <summary>Gets a value indicating whether the card describes a coding plan.</summary>
    public bool HasPlan => _info.Plan is not null;

    /// <summary>Gets one line per plan tier with its published request limits, or an empty list.</summary>
    public IReadOnlyList<string> PlanLines =>
        _info.Plan?.Tiers.Select(tier => ModelsCopy.PlanLine(tier.Name, tier.QuotaText)).ToArray() ?? [];

    /// <summary>Gets the line that says how long a paid period lasts, or null when the plan does not record it.</summary>
    public string? PlanValidityText => _info.Plan?.ValidityDays is { } days ? ModelsCopy.ValidityLine(days) : null;

    /// <summary>Gets the page where the plan is described, bought or renewed, or null.</summary>
    public Uri? PlanUrl => _info.Plan?.PlanUrl;

    /// <summary>Gets a value indicating whether an API key can be added or replaced: omp takes an API key for this provider.</summary>
    public bool CanSetApiKey => _info.AuthKind == ProviderAuthKind.ApiKey;

    /// <summary>Gets the label of the key action: Add API key while omp holds none, Replace API key otherwise.</summary>
    public string ApiKeyActionLabel => _info.Credential == CredentialState.Present ? ModelsCopy.ReplaceApiKeyLabel : ModelsCopy.AddApiKeyLabel;

    /// <summary>Gets the open key entry, or null while no entry is open.</summary>
    public ApiKeyDraft? ApiKey => _apiKey;

    /// <summary>Gets a value indicating whether the key entry is open.</summary>
    public bool IsApiKeyEditorOpen => _apiKey is not null;

    /// <summary>Gets a value indicating whether the provider can be edited: OmpOS wrote it as a custom provider.</summary>
    public bool CanEdit => _info is { IsOmpOSManaged: true, Group: ProviderGroup.Custom, Definition: not null };

    /// <summary>Gets a value indicating whether the provider can be removed: OmpOS wrote it.</summary>
    public bool CanRemove => _info is { IsOmpOSManaged: true, Group: ProviderGroup.Custom };

    /// <summary>Gets a value indicating whether the card asks the user to confirm removal.</summary>
    public bool IsConfirmingRemove { get => _isConfirmingRemove; private set => SetProperty(ref _isConfirmingRemove, value); }

    /// <summary>Gets the question of the removal confirmation.</summary>
    public string RemoveQuestion => ModelsCopy.RemoveQuestion(_info.DisplayName);

    /// <summary>Gets a value indicating whether a connection test is running.</summary>
    public bool IsTesting { get => _isTesting; private set => SetProperty(ref _isTesting, value); }

    /// <summary>Gets what the last connection test found, or null before one finished.</summary>
    public ProviderTestStatus? TestStatus { get => _testStatus; private set => SetProperty(ref _testStatus, value); }

    /// <summary>Gets the sentence for the last connection test, or null before one finished.</summary>
    public string? TestText { get => _testText; private set => SetProperty(ref _testText, value); }

    /// <summary>Gets a value indicating whether an action other than a test is running.</summary>
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    /// <summary>Gets the reason the last action failed, or null.</summary>
    public string? ErrorText { get => _errorText; private set => SetProperty(ref _errorText, value); }

    /// <summary>Gets the sentence that says the last action worked, or null.</summary>
    public string? ActionText { get => _actionText; private set => SetProperty(ref _actionText, value); }

    /// <summary>Opens the key entry. Nothing happens when the provider takes no API key.</summary>
    public void BeginApiKey()
    {
        if (!CanSetApiKey || _apiKey is not null)
        {
            return;
        }

        ErrorText = null;
        ActionText = null;
        SetApiKey(new ApiKeyDraft(_info.Id, _info.DisplayName, _info.IsOmpOSManaged));
    }

    /// <summary>Closes the key entry and clears what was typed.</summary>
    public void CancelApiKey()
    {
        _apiKey?.Clear();
        SetApiKey(null);
    }

    /// <summary>
    /// Hands the typed key to the source once. The typed text is cleared whether the source succeeds or fails, and no message the
    /// card shows contains the key. On success the entry closes and the page reads the list again; on failure the entry stays open,
    /// empty, with the source's message in <see cref="ErrorText"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when omp accepted the key.</returns>
    public async Task<bool> SaveApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var draft = _apiKey;
        if (draft is null || _isBusy)
        {
            return false;
        }

        if (!draft.IsValid)
        {
            ErrorText = draft.Error ?? ModelsCopy.ErrApiKeyRequired;
            return false;
        }

        var key = draft.TakeKey();
        ErrorText = null;
        ActionText = null;
        IsBusy = true;
        draft.IsSaving = true;
        ModelsActionResult result;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            result = await _source.SaveApiKeyAsync(_info.Id, key, cancellationToken).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            result = ModelsActionResult.Failure(exception.Message);
        }
        finally
        {
            draft.Clear();
            draft.IsSaving = false;
            IsBusy = false;
        }

        if (!result.Succeeded)
        {
            ErrorText = Hide(result.Message, key) ?? ModelsCopy.ActionFailed;
            return false;
        }

        SetApiKey(null);
        var saved = ModelsCopy.KeySaved(_info.DisplayName);
        ActionText = saved;
        await _host.ChangedAsync(saved, cancellationToken).ConfigureAwait(true);
        return true;
    }

    /// <summary>Makes a model of this provider the default for new chats. A selector the card does not list is refused.</summary>
    /// <param name="selector">The model as <c>provider/model</c>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when omp now uses the model as the default.</returns>
    public async Task<bool> UseAsDefaultAsync(string selector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (_isBusy || !_models.Any(row => string.Equals(row.Selector, selector, StringComparison.Ordinal) && row.CanUseAsDefault))
        {
            return false;
        }

        ErrorText = null;
        ActionText = null;
        IsBusy = true;
        ModelsActionResult result;
        try
        {
            result = await _source.SetDefaultModelAsync(selector, cancellationToken).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            result = ModelsActionResult.Failure(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }

        if (!result.Succeeded)
        {
            ErrorText = string.IsNullOrWhiteSpace(result.Message) ? ModelsCopy.ActionFailed : result.Message;
            return false;
        }

        ActionText = ModelsCopy.DefaultSet(selector);
        _host.DefaultChanged(selector);
        return true;
    }

    /// <summary>Tests the provider through the source and shows what it found. Cloud and plan providers say the test was not run unless the source returns a result.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task that completes when the outcome is shown.</returns>
    public async Task TestAsync(CancellationToken cancellationToken = default)
    {
        if (_isTesting)
        {
            return;
        }

        IsTesting = true;
        TestStatus = null;
        TestText = null;
        ProviderTestOutcome outcome;
        try
        {
            outcome = await _source.TestAsync(_info.Id, cancellationToken).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            outcome = new ProviderTestOutcome(ProviderTestStatus.Failed, exception.Message);
        }
        finally
        {
            IsTesting = false;
        }

        TestStatus = outcome.Status;
        var text = ModelsCopy.TestText(outcome.Status, outcome.Message, _info.Group);
        TestText = text;
        _host.Announce(ModelsCopy.TestAnnouncement(_info.DisplayName, text));
    }

    /// <summary>Opens the custom provider form for this provider. Nothing happens unless <see cref="CanEdit"/>.</summary>
    public void BeginEdit()
    {
        if (CanEdit)
        {
            _host.EditRequested(this);
        }
    }

    /// <summary>Asks the user to confirm removal. Nothing happens unless <see cref="CanRemove"/>.</summary>
    public void BeginRemove()
    {
        if (CanRemove)
        {
            ErrorText = null;
            IsConfirmingRemove = true;
        }
    }

    /// <summary>Withdraws the removal question.</summary>
    public void CancelRemove() => IsConfirmingRemove = false;

    /// <summary>Removes the provider after <see cref="BeginRemove"/>. Without the question nothing is removed.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when omp no longer has the provider.</returns>
    public async Task<bool> ConfirmRemoveAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConfirmingRemove || !CanRemove || _isBusy)
        {
            return false;
        }

        IsBusy = true;
        ModelsActionResult result;
        try
        {
            result = await _source.RemoveCustomProviderAsync(_info.Id, cancellationToken).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            result = ModelsActionResult.Failure(exception.Message);
        }
        finally
        {
            IsBusy = false;
            IsConfirmingRemove = false;
        }

        if (!result.Succeeded)
        {
            ErrorText = string.IsNullOrWhiteSpace(result.Message) ? ModelsCopy.ActionFailed : result.Message;
            return false;
        }

        await _host.ChangedAsync(ModelsCopy.CustomRemoved(_info.DisplayName), cancellationToken).ConfigureAwait(true);
        return true;
    }

    /// <summary>Describes the card without any entry.</summary>
    /// <returns>The provider id.</returns>
    public override string ToString() => $"ProviderCard({_info.Id})";

    /// <summary>Replaces the provider and the default after a read, keeping the state of the actions.</summary>
    /// <param name="info">The provider as read.</param>
    /// <param name="defaultModel">The default model selector, or null.</param>
    internal void Update(ProviderInfo info, string? defaultModel)
    {
        ArgumentNullException.ThrowIfNull(info);
        _info = info;
        _defaultModel = defaultModel;
        _models = BuildRows();
        NotifyPropertyChanged(string.Empty);
    }

    /// <summary>Marks the model that is now the default.</summary>
    /// <param name="defaultModel">The default model selector, or null.</param>
    internal void ApplyDefault(string? defaultModel)
    {
        _defaultModel = defaultModel;
        foreach (var row in _models)
        {
            row.IsDefault = string.Equals(row.Selector, defaultModel, StringComparison.Ordinal);
        }
    }

    private static string? Hide(string? message, string key)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return key.Length == 0 ? message : message.Replace(key, Hidden, StringComparison.Ordinal);
    }

    private ProviderModelRow[] BuildRows() =>
        [.. _info.Models.Select(selector => new ProviderModelRow(selector, string.Equals(selector, _defaultModel, StringComparison.Ordinal)))];

    private void SetApiKey(ApiKeyDraft? draft)
    {
        if (ReferenceEquals(_apiKey, draft))
        {
            return;
        }

        _apiKey = draft;
        NotifyPropertyChanged(nameof(ApiKey));
        NotifyPropertyChanged(nameof(IsApiKeyEditorOpen));
    }
}
