namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// A settings page over one immutable values record behind a source. The draft is edited freely; only <see cref="SaveAsync"/>
/// writes it to the source and only a successful write changes the saved values. While the page is not Ready (loading or failed
/// to load) there is no draft: edits, reset and discard do nothing and nothing is dirty.
/// </summary>
/// <typeparam name="TValues">The values record; compared by value to tell whether the draft changed.</typeparam>
public abstract class EditableSettingsPage<TValues> : SettingsPageBase
    where TValues : class
{
    private readonly TValues _defaults;
    private TValues _saved;
    private TValues _draft;

    /// <summary>Initializes a new instance of the <see cref="EditableSettingsPage{TValues}"/> class.</summary>
    /// <param name="section">The section the page fills.</param>
    /// <param name="title">The navigation label.</param>
    /// <param name="defaults">The values Reset returns the draft to; also the values shown until the first load.</param>
    /// <exception cref="ArgumentNullException"><paramref name="defaults"/> is null.</exception>
    protected EditableSettingsPage(SettingsSection section, string title, TValues defaults)
        : base(section, title, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        _defaults = defaults;
        _saved = defaults;
        _draft = defaults;
    }

    /// <summary>Gets the last saved values.</summary>
    public TValues Saved => _saved;

    /// <summary>Gets the draft values.</summary>
    public TValues Draft => _draft;

    /// <inheritdoc/>
    public override bool IsDirty => State == SettingsPageState.Ready && !EqualityComparer<TValues>.Default.Equals(_draft, _saved);

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(LoadCoreAsync, cancellationToken);

    /// <inheritdoc/>
    public override async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        if (!IsDirty)
        {
            return true;
        }

        var values = _draft;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            await SaveValuesAsync(values, cancellationToken).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            SetSaveError(exception.Message);
            return false;
        }

        _saved = values;
        SetSaveError(string.Empty);
        Publish();
        return true;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        if (State == SettingsPageState.Ready)
        {
            SetSaveError(string.Empty);
            SetDraft(_defaults);
        }
    }

    /// <inheritdoc/>
    public override void Discard()
    {
        base.Discard();
        if (State == SettingsPageState.Ready)
        {
            SetDraft(_saved);
        }
    }

    /// <summary>Reads the values from the source.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The saved values.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be read.</exception>
    protected abstract Task<TValues> LoadValuesAsync(CancellationToken cancellationToken);

    /// <summary>Writes the values to the source.</summary>
    /// <param name="values">The values to save.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the values are written.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be written.</exception>
    protected abstract Task SaveValuesAsync(TValues values, CancellationToken cancellationToken);

    /// <summary>Announces the page's typed value properties after the draft or the saved values changed.</summary>
    protected abstract void NotifyValueProperties();

    /// <summary>
    /// Replaces the draft while the page is Ready; any earlier save error is cleared. Setting the draft to what it already is changes
    /// nothing, so the save error stays: a control that reports the value it already shows is not an edit.
    /// </summary>
    /// <param name="values">The new draft.</param>
    /// <returns>True when the draft was replaced.</returns>
    protected bool SetDraft(TValues values)
    {
        if (State != SettingsPageState.Ready)
        {
            return false;
        }

        if (EqualityComparer<TValues>.Default.Equals(values, _draft))
        {
            return false;
        }

        _draft = values;
        SetSaveError(string.Empty);
        Publish();
        return true;
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        var values = await LoadValuesAsync(cancellationToken).ConfigureAwait(true);
        _saved = values;
        _draft = values;
        Publish();
    }

    private void Publish()
    {
        NotifyPropertyChanged(nameof(Saved));
        NotifyPropertyChanged(nameof(Draft));
        NotifyValueProperties();
        NotifyDirtyChanged();
    }
}
