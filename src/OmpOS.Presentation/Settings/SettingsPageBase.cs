using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// The shared state of a settings page: its section, title, Loading/Ready/Error state and inline save error. A page that has
/// nothing to edit keeps the defaults here: never dirty, saving is a no-op that succeeds, reset and discard change nothing.
/// </summary>
public abstract class SettingsPageBase : ObservableModel, ISettingsPage
{
    private SettingsPageState _state;
    private string _loadError = string.Empty;
    private string _saveError = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="SettingsPageBase"/> class.</summary>
    /// <param name="section">The section the page fills.</param>
    /// <param name="title">The navigation label.</param>
    /// <param name="initialState">The state the page starts in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="section"/> or <paramref name="initialState"/> is not defined.</exception>
    protected SettingsPageBase(SettingsSection section, string title, SettingsPageState initialState)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (!Enum.IsDefined(section))
        {
            throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown settings section.");
        }

        if (!Enum.IsDefined(initialState))
        {
            throw new ArgumentOutOfRangeException(nameof(initialState), initialState, "Unknown settings page state.");
        }

        Section = section;
        Title = title;
        _state = initialState;
    }

    /// <inheritdoc/>
    public SettingsSection Section { get; }

    /// <inheritdoc/>
    public string Title { get; }

    /// <summary>Gets the state the page is in.</summary>
    public SettingsPageState State => _state;

    /// <summary>Gets the reason the page's values failed to load, or an empty string when they did not.</summary>
    public string LoadError => _loadError;

    /// <summary>Gets the reason the latest save failed, or an empty string when it did not.</summary>
    public string SaveError => _saveError;

    /// <inheritdoc/>
    public virtual bool IsDirty => false;

    /// <inheritdoc/>
    public virtual bool HasError => _saveError.Length > 0;

    /// <inheritdoc/>
    public abstract Task LoadAsync(CancellationToken cancellationToken);

    /// <inheritdoc/>
    public virtual Task<bool> SaveAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    /// <inheritdoc/>
    public virtual void Reset()
    {
    }

    /// <inheritdoc/>
    public virtual void Discard() => SetSaveError(string.Empty);

    /// <summary>
    /// Runs a load: the page shows Loading, <paramref name="load"/> reads and applies the values, then the page shows Ready. A
    /// <see cref="SettingsSourceException"/> puts the page in the error state with its message; anything else escapes.
    /// </summary>
    /// <param name="load">Reads the values from the source and applies them to the page.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the page is ready or has failed to load.</returns>
    protected async Task RunLoadAsync(Func<CancellationToken, Task> load, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(load);

        SetSaveError(string.Empty);
        SetLoadState(SettingsPageState.Loading, string.Empty);
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            await load(cancellationToken).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            SetLoadState(SettingsPageState.Error, exception.Message);
            return;
        }

        SetLoadState(SettingsPageState.Ready, string.Empty);
    }

    /// <summary>Sets the inline save error. An empty string clears it.</summary>
    /// <param name="message">The message, or an empty string.</param>
    protected void SetSaveError(string message)
    {
        if (SetProperty(ref _saveError, message, nameof(SaveError)))
        {
            NotifyPropertyChanged(nameof(HasError));
        }
    }

    /// <summary>Announces that <see cref="IsDirty"/> and <see cref="HasError"/> may have changed.</summary>
    protected void NotifyDirtyChanged()
    {
        NotifyPropertyChanged(nameof(IsDirty));
        NotifyPropertyChanged(nameof(HasError));
    }

    /// <summary>Called after <see cref="State"/> changed, so a page can announce the properties that depend on it. Does nothing by default.</summary>
    protected virtual void OnStateChanged()
    {
    }

    private void SetLoadState(SettingsPageState state, string loadError)
    {
        SetProperty(ref _loadError, loadError, nameof(LoadError));
        SetProperty(ref _state, state, nameof(State));
        OnStateChanged();
        NotifyDirtyChanged();
    }
}
