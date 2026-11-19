using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Composer;

/// <summary>
/// The select state machine of the composer's engine-owned choosers, the model picker and the option chips. The engine owns the list of
/// values and the value in use; the chooser shows what the host reports and asks the host to change the value. It is disabled while a run
/// is active, while a change is in flight, and while it is hidden. A refused or failed change leaves the value as it was and sets
/// <see cref="Notice"/>, except that the engine's own answer wins when it names a value still in use.
/// </summary>
/// <typeparam name="TChoice">The value the engine offers.</typeparam>
/// <typeparam name="TResult">What the host answers when it is asked to change the value.</typeparam>
public abstract class EngineSelectModel<TChoice, TResult> : ObservableModel
    where TChoice : class
{
    private readonly string _changeSubject;
    private readonly Func<string, CancellationToken, Task<TResult>> _switchValue;
    private IReadOnlyList<TChoice> _choices = [];
    private TChoice? _selected;
    private string? _currentValue;
    private string? _notice;
    private bool _isInFlight;
    private bool _isRunActive;
    private bool _isTerminalSession;

    /// <summary>Creates the state machine of a chooser that knows no value yet.</summary>
    /// <param name="changeSubject">The name the notices use for the value, for example <c>The model</c> or <c>Mode</c>.</param>
    /// <param name="switchValue">Asks the host to give the option a value. It answers whether the engine confirmed the change.</param>
    /// <exception cref="ArgumentException"><paramref name="changeSubject"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="switchValue"/> is null.</exception>
    protected EngineSelectModel(string changeSubject, Func<string, CancellationToken, Task<TResult>> switchValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(changeSubject);
        ArgumentNullException.ThrowIfNull(switchValue);

        _changeSubject = changeSubject;
        _switchValue = switchValue;
        SelectCommand = new AgentPanelCommand(
            parameter => SelectAsync(parameter as TChoice),
            fault => Notice = $"{_changeSubject} could not be changed: {fault.Message}",
            _ => IsEnabled);
    }

    /// <summary>Gets the command that changes the value. Its parameter is a <typeparamref name="TChoice"/>; any other parameter does nothing.</summary>
    public AgentPanelCommand SelectCommand { get; }

    /// <summary>Gets the values the engine offers, in its order. Empty until the host reports them.</summary>
    public IReadOnlyList<TChoice> Choices => _choices;

    /// <summary>Gets the value in use, or <see langword="null"/> when it is not among <see cref="Choices"/>.</summary>
    public TChoice? SelectedChoice => _selected;

    /// <summary>Gets the engine's identifier of the value in use, or <see langword="null"/> when the host reported none.</summary>
    public string? CurrentValue => _currentValue;

    /// <summary>Gets the text of the chooser.</summary>
    public abstract string Label { get; }

    /// <summary>Gets a value indicating whether the chooser is shown.</summary>
    public abstract bool IsVisible { get; }

    /// <summary>Gets a value indicating whether the user can open the list and change the value: not while a run is active or a change is in flight, and not when the chooser is hidden.</summary>
    public bool IsEnabled => IsVisible && !_isRunActive && !_isInFlight;

    /// <summary>Gets the sentence for the chooser's tooltip: the reason it is disabled, or what it does.</summary>
    public abstract string ToolTipText { get; }

    /// <summary>Gets or sets whether a run is active. A run keeps the value fixed, so the chooser is disabled while it is.</summary>
    public bool IsRunActive
    {
        get => _isRunActive;
        set
        {
            if (SetProperty(ref _isRunActive, value))
            {
                NotifyEnablementChanged();
            }
        }
    }

    /// <summary>Gets or sets whether the session is an omp terminal. A terminal runs its own omp, which owns its settings, so the chooser is hidden.</summary>
    public bool IsTerminalSession
    {
        get => _isTerminalSession;
        set
        {
            if (SetProperty(ref _isTerminalSession, value))
            {
                NotifyPropertyChanged(nameof(IsVisible));
                NotifyEnablementChanged();
            }
        }
    }

    /// <summary>Gets the sentence that says why the last change did not happen, or <see langword="null"/>. A new attempt or a change that succeeds clears it.</summary>
    public string? Notice
    {
        get => _notice;
        protected set
        {
            if (SetProperty(ref _notice, value))
            {
                NotifyPropertyChanged(nameof(HasNotice));
            }
        }
    }

    /// <summary>Gets a value indicating whether <see cref="Notice"/> has text.</summary>
    public bool HasNotice => _notice is not null;

    /// <summary>Gets a value indicating whether a change is waiting for the engine.</summary>
    protected bool IsInFlight => _isInFlight;

    /// <summary>Gets the engine's identifier of a value.</summary>
    /// <param name="choice">The value.</param>
    /// <returns>The identifier the host sends back to change to the value.</returns>
    protected abstract string ValueOf(TChoice choice);

    /// <summary>Reads what the host answered.</summary>
    /// <param name="result">The answer of the host.</param>
    /// <returns>Whether the engine now uses the requested value, the value in use after the attempt, and the reason it did not change.</returns>
    protected abstract (bool Confirmed, string? CurrentValue, string? Message) ReadResult(TResult result);

    /// <summary>
    /// Changes the value to <paramref name="choice"/> through the host. Does nothing when the chooser is disabled, or the value is already in use.
    /// A refused or failed change leaves the value as it was and sets <see cref="Notice"/>.
    /// </summary>
    /// <param name="choice">The value to change to.</param>
    /// <returns>A task that completes when the host has answered. A fault of the host is thrown to the caller; <see cref="SelectCommand"/> turns it into the notice.</returns>
    public async Task SelectAsync(TChoice? choice)
    {
        if (choice is null || !IsEnabled)
        {
            return;
        }

        var value = ValueOf(choice);
        if (string.Equals(value, _currentValue, StringComparison.Ordinal))
        {
            Notice = null;
            return;
        }

        SetInFlight(true);
        Notice = null;
        try
        {
            var (confirmed, reported, message) = ReadResult(await _switchValue(value, CancellationToken.None).ConfigureAwait(true));
            if (confirmed)
            {
                SetCurrent(reported ?? value);
                return;
            }

            // The selection returns to the previous value: the engine's own answer wins when it names the value still in use.
            if (reported is { } stillInUse)
            {
                SetCurrent(stillInUse);
            }

            Notice = message ?? $"{_changeSubject} could not be changed.";
        }
        finally
        {
            SetInFlight(false);
        }
    }

    /// <summary>Replaces the values and the value in use with what the host reports. The notice stays.</summary>
    /// <param name="choices">The values the engine offers; empty when it offers none.</param>
    /// <param name="currentValue">The identifier of the value in use, or null when none is reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="choices"/> is null.</exception>
    protected void ReplaceChoices(IReadOnlyList<TChoice> choices, string? currentValue)
    {
        ArgumentNullException.ThrowIfNull(choices);

        _choices = choices;
        _currentValue = currentValue;
        _selected = FindSelected();
        NotifyPropertyChanged(nameof(Choices));
        NotifyCurrentChanged();
    }

    /// <summary>Makes <paramref name="value"/> the value in use and notifies the change.</summary>
    /// <param name="value">The identifier of the value now in use.</param>
    protected virtual void SetCurrent(string value)
    {
        _currentValue = value;
        _selected = FindSelected();
        NotifyCurrentChanged();
    }

    /// <summary>Raises the notifications of the value in use.</summary>
    protected virtual void NotifyCurrentChanged()
    {
        NotifyPropertyChanged(nameof(CurrentValue));
        NotifyPropertyChanged(nameof(SelectedChoice));
        NotifyPropertyChanged(nameof(Label));
        NotifyPropertyChanged(nameof(ToolTipText));
    }

    /// <summary>Raises the notifications of whether the chooser can be used.</summary>
    protected void NotifyEnablementChanged()
    {
        NotifyPropertyChanged(nameof(IsEnabled));
        NotifyPropertyChanged(nameof(ToolTipText));
        SelectCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Raises the notifications of a change of <see cref="IsInFlight"/>.</summary>
    protected virtual void OnInFlightChanged() => NotifyEnablementChanged();

    private TChoice? FindSelected()
    {
        foreach (var choice in _choices)
        {
            if (string.Equals(ValueOf(choice), _currentValue, StringComparison.Ordinal))
            {
                return choice;
            }
        }

        return null;
    }

    private void SetInFlight(bool value)
    {
        if (_isInFlight == value)
        {
            return;
        }

        _isInFlight = value;
        OnInFlightChanged();
    }
}
