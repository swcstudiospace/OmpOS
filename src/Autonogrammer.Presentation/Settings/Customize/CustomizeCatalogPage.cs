using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Settings.Customize;

/// <summary>
/// The shared model of a read-only Customize page (Rules, Skills, Commands): a catalogue of entries with title, scope, origin and a
/// short description, in one of four distinct states. The page has no edit action and nothing to save: it is never dirty and never
/// in error, saving succeeds with no effect and reset and discard change nothing. An expected source failure
/// (<see cref="SettingsSourceException"/>) becomes <see cref="CustomizePageState.Error"/>; cancelling a load throws
/// <see cref="OperationCanceledException"/> and leaves the page as it was.
/// </summary>
public abstract class CustomizeCatalogPage : ObservableModel, ISettingsPage
{
    private CustomizePageState _state = CustomizePageState.Loading;
    private IReadOnlyList<CustomizeEntry> _entries = [];
    private IReadOnlyList<string> _problems = [];
    private string? _loadFailure;

    /// <summary>Initializes a new instance of the <see cref="CustomizeCatalogPage"/> class.</summary>
    /// <param name="section">The section the page fills.</param>
    /// <param name="title">The navigation label.</param>
    /// <param name="introText">The sentence under the heading.</param>
    /// <param name="emptyText">The explanation of the empty state.</param>
    protected CustomizeCatalogPage(SettingsSection section, string title, string introText, string emptyText)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(introText);
        ArgumentNullException.ThrowIfNull(emptyText);
        Section = section;
        Title = title;
        IntroText = introText;
        EmptyText = emptyText;
    }

    /// <inheritdoc/>
    public SettingsSection Section { get; }

    /// <inheritdoc/>
    public string Title { get; }

    /// <summary>Gets the sentence under the heading.</summary>
    public string IntroText { get; }

    /// <summary>Gets the explanation of the empty state.</summary>
    public string EmptyText { get; }

    /// <summary>Gets the state the page is in.</summary>
    public CustomizePageState State => _state;

    /// <summary>Gets the entries.</summary>
    public IReadOnlyList<CustomizeEntry> Entries => _entries;

    /// <summary>Gets one sentence per file that could not be read.</summary>
    public IReadOnlyList<string> Problems => _problems;

    /// <summary>Gets the reason the catalogue could not be read, or null.</summary>
    public string? LoadFailure => _loadFailure;

    /// <summary>Gets the text for a live region: how many entries the page lists.</summary>
    public string CountText => CustomizeCopy.CountText(_entries.Count);

    /// <inheritdoc/>
    public bool IsDirty => false;

    /// <inheritdoc/>
    public bool HasError => false;

    /// <inheritdoc/>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var previous = _state;
        SetState(CustomizePageState.Loading);
        SetLoadFailure(null);
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            var catalog = await LoadCatalogAsync(cancellationToken).ConfigureAwait(true);
            _entries = catalog.Entries;
            _problems = catalog.Problems;
            NotifyPropertyChanged(nameof(Entries));
            NotifyPropertyChanged(nameof(Problems));
            NotifyPropertyChanged(nameof(CountText));
            SetState(catalog.Entries.Count == 0 ? CustomizePageState.Empty : CustomizePageState.Ready);
        }
        catch (SettingsSourceException exception)
        {
            SetLoadFailure(string.IsNullOrWhiteSpace(exception.Message) ? CustomizeCopy.LoadFailed : exception.Message);
            SetState(CustomizePageState.Error);
        }
        catch (OperationCanceledException)
        {
            SetState(previous);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<bool> SaveAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    /// <inheritdoc/>
    public void Reset()
    {
    }

    /// <inheritdoc/>
    public void Discard()
    {
    }

    /// <summary>Reads the catalogue from the page's source.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The catalogue.</returns>
    protected abstract Task<CustomizeCatalog> LoadCatalogAsync(CancellationToken cancellationToken);

    private void SetState(CustomizePageState state) => SetProperty(ref _state, state, nameof(State));

    private void SetLoadFailure(string? failure) => SetProperty(ref _loadFailure, failure, nameof(LoadFailure));
}
