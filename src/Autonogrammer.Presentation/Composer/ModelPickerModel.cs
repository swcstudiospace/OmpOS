namespace Autonogrammer.Presentation.Composer;

/// <summary>
/// The model chip of the composer and the list behind it. The engine owns the list of models and the current model: the picker shows
/// what the host reports through <see cref="Refresh"/> and asks the host to change the current model through the delegate it was
/// built with. A refused or failed switch leaves the current model as it was and sets <c>Notice</c>. Omp can offer hundreds of
/// models, so the list is grouped by provider and filtered by <see cref="FilterText"/>, and the keyboard cursor
/// (<see cref="HighlightedRow"/>) moves over the models that match.
/// </summary>
/// <remarks>The picker runs on the UI context that owns the other presentation models and continues on it after each await.</remarks>
public sealed class ModelPickerModel : EngineSelectModel<ModelChoice, ModelSwitchResult>
{
    /// <summary>The label of the chip while the models are not known yet.</summary>
    public const string PendingLabel = "Model";

    private const string RunningReason = "The model cannot be changed while a response is running.";

    private readonly Func<CancellationToken, Task>? _load;
    private List<ModelPickerRow> _rows = [];
    private ModelPickerRow? _highlighted;
    private string _filterText = string.Empty;
    private bool _isLoaded;
    private bool _isLoading;

    /// <summary>Creates a picker that knows no model yet.</summary>
    /// <param name="switchModel">Asks the host to switch the model of the session to a <see cref="ModelChoice.Value"/>. It answers whether the engine confirmed the switch.</param>
    /// <param name="load">
    /// Asks the host to start the engine session so that it reports its models, or null when the host reports them without being asked. With a
    /// loader the chip shows <see cref="PendingLabel"/> until <see cref="EnsureLoadedAsync"/> has run.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="switchModel"/> is null.</exception>
    public ModelPickerModel(Func<string, CancellationToken, Task<ModelSwitchResult>> switchModel, Func<CancellationToken, Task>? load = null)
        : base("The model", switchModel)
    {
        _load = load;
        _isLoaded = load is null;
    }

    /// <summary>Gets the label of the chip: the provider and the model, <see cref="PendingLabel"/> while the models are not known, or the raw
    /// identifier when the engine runs a model it does not list.</summary>
    public override string Label
    {
        get
        {
            if (SelectedChoice is { } choice)
            {
                return string.Equals(choice.Provider, ModelChoice.OtherProvider, StringComparison.Ordinal) ? choice.Name : $"{choice.Provider} / {choice.Name}";
            }

            return CurrentValue ?? PendingLabel;
        }
    }

    /// <summary>Gets a value indicating whether the chip is shown: never in a terminal session, and not once the engine has reported that it offers no model.</summary>
    public override bool IsVisible => !IsTerminalSession && (!_isLoaded || Choices.Count > 0);

    /// <summary>Gets the sentence for the chip's tooltip and help text: the reason the chip is disabled, or what it does.</summary>
    public override string ToolTipText => IsRunActive ? RunningReason : IsSwitching ? "Switching the model." : $"Model: {Label}. Choose another model.";

    /// <summary>Gets a value indicating whether the host has reported its models (or none were to be waited for).</summary>
    public bool IsLoaded
    {
        get => _isLoaded;
        private set
        {
            if (SetProperty(ref _isLoaded, value))
            {
                NotifyPropertyChanged(nameof(IsVisible));
                NotifyEnablementChanged();
            }
        }
    }

    /// <summary>Gets a value indicating whether the host is starting the engine session to learn its models.</summary>
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    /// <summary>Gets a value indicating whether a switch is waiting for the engine.</summary>
    public bool IsSwitching => IsInFlight;

    /// <summary>Gets or sets the text the list is filtered by. Every word must occur in a model's name or identifier, ignoring case. Blank shows every model.</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value ?? string.Empty))
            {
                RebuildRows();
            }
        }
    }

    /// <summary>Gets the rows of the list: a heading per provider (alphabetical) followed by that provider's models that match <see cref="FilterText"/>, in the engine's order.</summary>
    public IReadOnlyList<ModelPickerRow> Rows => _rows;

    /// <summary>Gets a value indicating whether the filter hides every model.</summary>
    public bool HasNoMatches => Choices.Count > 0 && _rows.Count == 0;

    /// <summary>Gets the model row the keyboard cursor is on, or <see langword="null"/> when no model matches.</summary>
    public ModelPickerRow? HighlightedRow => _highlighted;

    /// <summary>
    /// Replaces the models and the current model with what the host reports, and marks the models as known. The label follows a change
    /// the engine made on its own. The notice stays.
    /// </summary>
    /// <param name="choices">The models the engine offers; empty when it offers none.</param>
    /// <param name="currentValue">The identifier of the model in use, or null when none is reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="choices"/> is null.</exception>
    public void Refresh(IReadOnlyList<ModelChoice> choices, string? currentValue)
    {
        ReplaceChoices(choices, currentValue);
        RebuildRows();
        IsLoaded = true;
        NotifyPropertyChanged(nameof(IsVisible));
        NotifyEnablementChanged();
    }

    /// <summary>
    /// Forgets the models, as when the engine session behind the picker ended. A picker with a loader goes back to not knowing its models,
    /// so the chip reads <see cref="PendingLabel"/> and the next open asks the host again; a picker without one has nothing to offer and hides.
    /// </summary>
    public void Reset()
    {
        ReplaceChoices([], null);
        RebuildRows();
        IsLoaded = _load is null;
        NotifyPropertyChanged(nameof(IsVisible));
        NotifyEnablementChanged();
    }

    /// <summary>
    /// Asks the host to start the engine session so that it reports its models. Does nothing when the models are known, a load is running,
    /// or no loader was given. A failed load sets <c>Notice</c> and leaves the models unknown, so the next call tries again.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the host answered.</returns>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_load is null || _isLoaded || _isLoading)
        {
            return;
        }

        IsLoading = true;
        Notice = null;
        try
        {
            await _load(cancellationToken).ConfigureAwait(true);

            // The host reports the models through Refresh while it loads; a host that reported none still ends the wait.
            IsLoaded = true;
        }
        catch (Exception fault) when (fault is not (OperationCanceledException or OutOfMemoryException))
        {
            Notice = $"The models could not be loaded: {fault.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Switches to the highlighted model, as pressing Enter in the list does.</summary>
    /// <returns>A task that completes when the host has answered. A fault becomes the notice.</returns>
    public Task ConfirmHighlightedAsync() => SelectCommand.ExecuteAsync(_highlighted?.Choice);

    /// <summary>Moves the keyboard cursor over the models that match. It stops at the first and last model and skips provider headings.</summary>
    /// <param name="delta">How many models to move; negative moves up.</param>
    public void MoveHighlight(int delta)
    {
        var current = _highlighted is null ? -1 : IndexOfChoiceRow(_highlighted);
        var count = ChoiceRowCount();
        if (count == 0)
        {
            return;
        }

        // From no cursor, down lands on the first model and up on the last.
        var target = current < 0 ? (delta >= 0 ? 0 : count - 1) : Math.Clamp(current + delta, 0, count - 1);
        SetHighlight(ChoiceRowAt(target));
    }

    /// <summary>Puts the keyboard cursor on a model row, for example the one under the pointer. A heading, or a row of another list, is ignored.</summary>
    /// <param name="row">The row to highlight.</param>
    public void Highlight(ModelPickerRow? row)
    {
        if (row is { Choice: not null } && _rows.Contains(row))
        {
            SetHighlight(row);
        }
    }

    /// <inheritdoc/>
    protected override string ValueOf(ModelChoice choice) => choice.Value;

    /// <inheritdoc/>
    protected override (bool Confirmed, string? CurrentValue, string? Message) ReadResult(ModelSwitchResult result) =>
        (result.IsSwitched, result.CurrentValue, result.Message);

    /// <inheritdoc/>
    protected override void SetCurrent(string value)
    {
        base.SetCurrent(value);
        RebuildRows();
    }

    /// <inheritdoc/>
    protected override void OnInFlightChanged()
    {
        NotifyPropertyChanged(nameof(IsSwitching));
        base.OnInFlightChanged();
    }

    private void SetHighlight(ModelPickerRow? row)
    {
        if (ReferenceEquals(_highlighted, row))
        {
            return;
        }

        if (_highlighted is not null)
        {
            _highlighted.IsHighlighted = false;
        }

        _highlighted = row;
        if (row is not null)
        {
            row.IsHighlighted = true;
        }

        NotifyPropertyChanged(nameof(HighlightedRow));
    }

    private void RebuildRows()
    {
        var words = _filterText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rows = new List<ModelPickerRow>();
        foreach (var group in Choices.Where(choice => Matches(choice, words)).GroupBy(static choice => choice.Provider, StringComparer.Ordinal).OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(ModelPickerRow.Heading(group.Key));
            foreach (var choice in group)
            {
                rows.Add(ModelPickerRow.ForChoice(choice, string.Equals(choice.Value, CurrentValue, StringComparison.Ordinal)));
            }
        }

        // The cursor stays on the model it was on, falls back to the model in use, and otherwise starts on the first match.
        var keep = _highlighted?.Choice?.Value;
        _highlighted = null;
        _rows = rows;
        var cursor = FirstChoiceRow(rows, keep) ?? FirstChoiceRow(rows, CurrentValue) ?? FirstChoiceRow(rows, null);
        if (cursor is not null)
        {
            cursor.IsHighlighted = true;
            _highlighted = cursor;
        }

        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(HighlightedRow));
        NotifyPropertyChanged(nameof(HasNoMatches));
    }

    private static bool Matches(ModelChoice choice, string[] words)
    {
        foreach (var word in words)
        {
            if (!choice.Name.Contains(word, StringComparison.OrdinalIgnoreCase) && !choice.Value.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds the model row for <paramref name="value"/>, or the first model row when <paramref name="value"/> is null.</summary>
    private static ModelPickerRow? FirstChoiceRow(List<ModelPickerRow> rows, string? value)
    {
        foreach (var row in rows)
        {
            if (row.Choice is { } choice && (value is null || string.Equals(choice.Value, value, StringComparison.Ordinal)))
            {
                return row;
            }
        }

        return null;
    }

    private int ChoiceRowCount() => _rows.Count(static row => !row.IsHeading);

    private int IndexOfChoiceRow(ModelPickerRow row)
    {
        var index = 0;
        foreach (var candidate in _rows)
        {
            if (candidate.IsHeading)
            {
                continue;
            }

            if (ReferenceEquals(candidate, row))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private ModelPickerRow ChoiceRowAt(int index) => _rows.Where(static row => !row.IsHeading).ElementAt(index);
}
