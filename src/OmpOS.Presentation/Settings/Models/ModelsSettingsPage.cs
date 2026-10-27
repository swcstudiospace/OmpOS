using System.ComponentModel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Settings.Models;

/// <summary>
/// The Models page of the settings window: every provider omp can use, grouped (local, cloud, coding plan, custom, omp built-in)
/// and filterable by a search box, with the actions the <see cref="IModelsSettingsSource"/> can back. The only edit the page
/// drafts is a custom provider; every other action (add or replace a key, use as default, test, remove) is written when the user
/// takes it. Saving the page writes the custom provider draft; <see cref="Reset"/> and <see cref="Discard"/> close it.
/// </summary>
/// <remarks>
/// <para>
/// Expected failures (<see cref="SettingsSourceException"/> and failed <see cref="ModelsActionResult"/> values) become
/// <see cref="State"/>, <see cref="LoadFailure"/>, <see cref="Notice"/>, <see cref="DraftError"/> and card error text; they are
/// never thrown. Cancelling a load throws <see cref="OperationCanceledException"/> and leaves the page as it was.
/// </para>
/// <para>Threading: continuations run on the captured context, which is the UI context when a head starts them.</para>
/// </remarks>
public sealed class ModelsSettingsPage : ObservableModel, ISettingsPage, IProviderCardHost
{
    private static readonly ProviderGroup[] GroupOrder =
    [
        ProviderGroup.Local,
        ProviderGroup.Cloud,
        ProviderGroup.CodingPlan,
        ProviderGroup.Custom,
        ProviderGroup.OmpBuiltIn,
    ];

    private readonly IModelsSettingsSource _source;
    private readonly List<ProviderCardModel> _cards = [];
    private readonly ProviderDirectoryModel _directory;
    private ModelsPageState _state = ModelsPageState.Loading;
    private string? _loadFailure;
    private string? _warning;
    private bool _isComplete = true;
    private string? _defaultModel;
    private string _searchText = string.Empty;
    private List<ProviderGroupModel> _groups = [];
    private IReadOnlyList<ProviderTemplate> _templates = [];
    private string? _notice;
    private string _announcement = string.Empty;
    private CustomProviderDraft? _draft;
    private string? _draftError;
    private bool _isSaving;

    /// <summary>Initializes a new instance of the <see cref="ModelsSettingsPage"/> class.</summary>
    /// <param name="source">The source that reads and writes omp's providers.</param>
    public ModelsSettingsPage(IModelsSettingsSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _directory = new ProviderDirectoryModel(source, this);
    }

    /// <inheritdoc/>
    public SettingsSection Section => SettingsSection.Models;

    /// <inheritdoc/>
    public string Title => ModelsCopy.PageTitle;

    /// <summary>Gets the state the page is in.</summary>
    public ModelsPageState State => _state;

    /// <summary>Gets the reason the providers could not be read, or null.</summary>
    public string? LoadFailure => _loadFailure;

    /// <summary>Gets omp's configuration warning (for example its <c>models.yml</c> validation message), or null.</summary>
    public string? Warning => _warning;

    /// <summary>Gets a value indicating whether the list is complete. When false omp lists only providers it holds a credential for.</summary>
    public bool IsComplete => _isComplete;

    /// <summary>Gets the sentence that says the list may be shorter than what omp supports, or null when the list is complete.</summary>
    public string? IncompleteNote => _isComplete ? null : ModelsCopy.IncompleteNote;

    /// <summary>Gets the default model selector, or null when none is set.</summary>
    public string? DefaultModel => _defaultModel;

    /// <summary>Gets or sets the search text. The list keeps the providers whose name, id, endpoint, group or a model contains it.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                _directory.SearchText = _searchText;
                RebuildGroups();
                Announcement = ModelsCopy.MatchCount(VisibleCardCount + _directory.VisibleCount);
            }
        }
    }

    /// <summary>Gets the provider listing: every provider, aggregator, coding plan and local runtime the page offers to set up, with region and kind filters.</summary>
    public ProviderDirectoryModel Directory => _directory;

    /// <summary>Gets the groups after the search filter, in the fixed group order. A group without a card left is absent.</summary>
    public IReadOnlyList<ProviderGroupModel> Groups => _groups;

    /// <summary>Gets the number of cards the filter keeps.</summary>
    public int VisibleCardCount => _groups.Sum(group => group.Cards.Count);

    /// <summary>Gets a value indicating whether the page is ready and the search keeps no card.</summary>
    public bool HasNoMatches => _state == ModelsPageState.Ready && _groups.Count == 0 && _directory.VisibleCount == 0;

    /// <summary>Gets the sentence that says the last list refresh after a change failed, or null.</summary>
    public string? Notice => _notice;

    /// <summary>Gets the text for a live region: the latest change, test or filter result. It never contains a key.</summary>
    public string Announcement { get => _announcement; private set => SetProperty(ref _announcement, value); }

    /// <summary>Gets the templates a custom provider can start from, in the order the source lists them.</summary>
    public IReadOnlyList<ProviderTemplate> Templates => _templates;

    /// <summary>Gets a value indicating whether there is a template to start a provider from.</summary>
    public bool HasTemplates => _templates.Count > 0;

    /// <summary>Gets the open custom provider form, or null.</summary>
    public CustomProviderDraft? Draft => _draft;

    /// <summary>Gets a value indicating whether the custom provider form is open.</summary>
    public bool IsDraftOpen => _draft is not null;

    /// <summary>Gets the reason the last save of the draft failed, or null.</summary>
    public string? DraftError => _draftError;

    /// <inheritdoc/>
    public bool IsDirty => _draft?.IsDirty ?? false;

    /// <inheritdoc/>
    public bool HasError => _draft is { IsDirty: true } draft && (!draft.IsValid || _draftError is not null);

    /// <inheritdoc/>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var previous = _state;
        SetState(ModelsPageState.Loading);
        SetLoadFailure(null);
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            var snapshot = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
            Apply(snapshot);
        }
        catch (SettingsSourceException exception)
        {
            SetLoadFailure(string.IsNullOrWhiteSpace(exception.Message) ? ModelsCopy.LoadFailed : exception.Message);
            SetState(ModelsPageState.Error);
        }
        catch (OperationCanceledException)
        {
            SetState(previous);
            throw;
        }
    }

    /// <summary>Opens a blank custom provider form. When one is open already it stays as it is.</summary>
    public void BeginAddCustom()
    {
        if (_draft is not null || _state is ModelsPageState.Loading or ModelsPageState.Error)
        {
            return;
        }

        OpenDraft(new CustomProviderDraft(_cards.Select(card => card.Id), (CustomProviderForm?)null));
    }

    /// <summary>
    /// Opens the custom provider form prefilled from a template: display name, an id slug made from the template id (numbered when
    /// another provider already uses it), protocol, endpoint, authentication and routes. The model list starts empty
    /// and plain http to a remote host is off. The saved provider is an ordinary custom provider whose key is added on its card.
    /// When a form is open already it stays as it is.
    /// </summary>
    /// <param name="templateId">The <see cref="ProviderTemplate.Id"/> of the template to start from.</param>
    /// <exception cref="ArgumentException">No template has that id.</exception>
    public void BeginAddCustomFromTemplate(string templateId)
    {
        ArgumentException.ThrowIfNullOrEmpty(templateId);
        if (_state is ModelsPageState.Loading or ModelsPageState.Error)
        {
            return;
        }

        var template = _templates.FirstOrDefault(candidate => string.Equals(candidate.Id, templateId, StringComparison.Ordinal))
            ?? throw new ArgumentException("No provider template has this id.", nameof(templateId));
        if (_draft is not null)
        {
            return;
        }

        OpenDraft(new CustomProviderDraft(_cards.Select(card => card.Id), template));
    }

    /// <summary>
    /// Opens the form for an OmpOS managed custom provider. An unknown or unmanaged provider is ignored, and so is the request while a
    /// form is open: the open form is never dropped silently; save or discard it first.
    /// </summary>
    /// <param name="providerId">The provider to edit.</param>
    public void BeginEdit(string providerId)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        var card = _cards.Find(candidate => string.Equals(candidate.Id, providerId, StringComparison.Ordinal));
        if (_draft is null && card is { CanEdit: true })
        {
            OpenEdit(card);
        }
    }

    /// <summary>
    /// Saves the custom provider form through the source and reads the list again. With no form open there is nothing to save. A form
    /// with a broken rule shows its errors and is not sent.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when the form was saved or there was nothing to save; false when it is rejected or omp refused it.</returns>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        var draft = _draft;
        if (draft is null)
        {
            return true;
        }

        if (_isSaving)
        {
            return false;
        }

        draft.ShowAllErrors();
        var form = draft.TryBuildForm();
        if (form is null)
        {
            NotifyDraftState();
            return false;
        }

        SetDraftError(null);
        _isSaving = true;
        ModelsActionResult result;
        try
        {
            result = await _source.SaveCustomProviderAsync(form, cancellationToken).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            result = ModelsActionResult.Failure(exception.Message);
        }
        finally
        {
            _isSaving = false;
        }

        if (!result.Succeeded)
        {
            SetDraftError(string.IsNullOrWhiteSpace(result.Message) ? ModelsCopy.ActionFailed : result.Message);
            return false;
        }

        CloseDraft();
        await ChangedAsync(ModelsCopy.CustomSaved(form.DisplayName), cancellationToken).ConfigureAwait(true);
        return true;
    }

    /// <inheritdoc/>
    public void Reset() => CloseDraft();

    /// <inheritdoc/>
    public void Discard() => CloseDraft();

    /// <summary>Makes a model the default for new chats, whichever card lists it.</summary>
    /// <param name="selector">The model as <c>provider/model</c>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when omp now uses it; false when no card lists it or omp refused it.</returns>
    public Task<bool> SetDefaultModelAsync(string selector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var card = _cards.Find(candidate => candidate.Models.Any(row => string.Equals(row.Selector, selector, StringComparison.Ordinal)));
        return card is null ? Task.FromResult(false) : card.UseAsDefaultAsync(selector, cancellationToken);
    }

    /// <inheritdoc/>
    Task IProviderCardHost.ChangedAsync(string announcement, CancellationToken cancellationToken) =>
        ChangedAsync(announcement, cancellationToken);

    /// <inheritdoc/>
    void IProviderCardHost.DefaultChanged(string selector)
    {
        SetDefault(selector);
        Announce(ModelsCopy.DefaultSet(selector));
    }

    /// <inheritdoc/>
    void IProviderCardHost.EditRequested(ProviderCardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (_draft is null)
        {
            OpenEdit(card);
        }
    }

    /// <inheritdoc/>
    void IProviderCardHost.Announce(string text) => Announce(text);

    private static bool Matches(ProviderCardModel card, string text) =>
        Contains(card.DisplayName, text)
        || Contains(card.Id, text)
        || Contains(card.EndpointText, text)
        || Contains(ModelsCopy.GroupTitle(card.Group), text)
        || card.Models.Any(row => Contains(row.Selector, text));

    private static bool Contains(string value, string text) => value.Contains(text, StringComparison.OrdinalIgnoreCase);

    private async Task ChangedAsync(string announcement, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
            Apply(snapshot);
            SetNotice(null);
        }
        catch (SettingsSourceException exception)
        {
            SetNotice(ModelsCopy.RefreshFailed(exception.Message));
        }

        Announce(announcement);
    }

    private void Apply(ModelsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var existing = _cards.ToDictionary(card => card.Id, StringComparer.Ordinal);
        _cards.Clear();
        foreach (var info in snapshot.Providers)
        {
            if (existing.TryGetValue(info.Id, out var card))
            {
                card.Update(info, snapshot.DefaultModel);
            }
            else
            {
                card = new ProviderCardModel(info, snapshot.DefaultModel, _source, this);
            }

            _cards.Add(card);
        }

        SetDefault(snapshot.DefaultModel);
        if (!string.Equals(_warning, snapshot.Warning, StringComparison.Ordinal))
        {
            _warning = snapshot.Warning;
            NotifyPropertyChanged(nameof(Warning));
        }

        if (_isComplete != snapshot.IsComplete)
        {
            _isComplete = snapshot.IsComplete;
            NotifyPropertyChanged(nameof(IsComplete));
            NotifyPropertyChanged(nameof(IncompleteNote));
        }

        RebuildGroups();

        _templates = snapshot.Templates;
        NotifyPropertyChanged(nameof(Templates));
        NotifyPropertyChanged(nameof(HasTemplates));
        _directory.Update(snapshot.Listings ?? [], _cards);
        SetState(_cards.Count == 0 && !_directory.HasListings ? ModelsPageState.Empty : ModelsPageState.Ready);
        NotifyPropertyChanged(nameof(HasNoMatches));
    }

    private void SetDefault(string? selector)
    {
        foreach (var card in _cards)
        {
            card.ApplyDefault(selector);
        }

        if (SetProperty(ref _defaultModel, selector, nameof(DefaultModel)))
        {
            RebuildGroups();
        }
    }

    private void RebuildGroups()
    {
        var text = _searchText.Trim();
        var groups = new List<ProviderGroupModel>();
        foreach (var group in GroupOrder)
        {
            var cards = _cards.Where(card => card.Group == group && (text.Length == 0 || Matches(card, text))).ToArray();
            if (cards.Length > 0)
            {
                groups.Add(new ProviderGroupModel(group, cards));
            }
        }

        _groups = groups;
        NotifyPropertyChanged(nameof(Groups));
        NotifyPropertyChanged(nameof(VisibleCardCount));
        NotifyPropertyChanged(nameof(HasNoMatches));
    }

    private void OpenEdit(ProviderCardModel card)
    {
        var taken = _cards.Where(other => !ReferenceEquals(other, card)).Select(other => other.Id);
        OpenDraft(new CustomProviderDraft(taken, card.Info.Definition));
    }

    private void OpenDraft(CustomProviderDraft draft)
    {
        CloseDraft();
        _draft = draft;
        draft.PropertyChanged += OnDraftChanged;
        SetDraftError(null);
        NotifyDraftState();
        NotifyPropertyChanged(nameof(Draft));
        NotifyPropertyChanged(nameof(IsDraftOpen));
    }

    private void CloseDraft()
    {
        if (_draft is null)
        {
            SetDraftError(null);
            return;
        }

        _draft.PropertyChanged -= OnDraftChanged;
        _draft = null;
        SetDraftError(null);
        NotifyDraftState();
        NotifyPropertyChanged(nameof(Draft));
        NotifyPropertyChanged(nameof(IsDraftOpen));
    }

    private void OnDraftChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_draftError is not null)
        {
            _draftError = null;
            NotifyPropertyChanged(nameof(DraftError));
        }

        NotifyDraftState();
    }

    private void NotifyDraftState()
    {
        NotifyPropertyChanged(nameof(IsDirty));
        NotifyPropertyChanged(nameof(HasError));
    }

    private void SetDraftError(string? error)
    {
        if (SetProperty(ref _draftError, error, nameof(DraftError)))
        {
            NotifyPropertyChanged(nameof(HasError));
        }
    }

    private void Announce(string text) => Announcement = text;

    private void SetNotice(string? notice) => SetProperty(ref _notice, notice, nameof(Notice));

    private void SetLoadFailure(string? failure) => SetProperty(ref _loadFailure, failure, nameof(LoadFailure));

    private void SetState(ModelsPageState state)
    {
        if (SetProperty(ref _state, state, nameof(State)))
        {
            NotifyPropertyChanged(nameof(HasNoMatches));
        }
    }
}
