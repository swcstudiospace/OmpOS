using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Composer;

/// <summary>
/// A chip of the composer for one select-style option of the session that the engine owns, for example the mode (<c>default</c> or
/// <c>plan</c>) or the thinking level. The engine owns the list of values and the current value: the chip shows what the host reports
/// through <see cref="Refresh"/> and asks the host to change the value through the delegate it was built with. The chip is enabled only
/// while no run is active; a refused or failed change leaves the value as it was and sets <see cref="Notice"/>.
/// </summary>
/// <remarks>The chip runs on the UI context that owns the other presentation models and continues on it after each await.</remarks>
public sealed class OptionChipModel : ObservableModel
{
    private const string RunningReason = "This setting cannot be changed while a response is running.";

    private readonly Func<string, CancellationToken, Task<OptionChangeResult>> _setValue;
    private readonly string _displayName;
    private IReadOnlyList<OptionChoice> _choices = [];
    private OptionChoice? _selected;
    private string? _currentValue;
    private string? _notice;
    private bool _isChanging;
    private bool _isRunActive;
    private bool _isTerminalSession;

    /// <summary>Creates a chip that knows no value yet.</summary>
    /// <param name="optionId">The engine's identifier of the option, for example <c>mode</c>.</param>
    /// <param name="displayName">The name of the option as the chip's tooltip says it, for example <c>Mode</c>.</param>
    /// <param name="setValue">Asks the host to give the option a <see cref="OptionChoice.Value"/>. It answers whether the engine confirmed the change.</param>
    /// <exception cref="ArgumentException"><paramref name="optionId"/> or <paramref name="displayName"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="setValue"/> is null.</exception>
    public OptionChipModel(string optionId, string displayName, Func<string, CancellationToken, Task<OptionChangeResult>> setValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(setValue);

        OptionId = optionId;
        _displayName = displayName;
        _setValue = setValue;
        SelectCommand = new AgentPanelCommand(
            parameter => SelectAsync(parameter as OptionChoice),
            fault => Notice = $"{_displayName} could not be changed: {fault.Message}",
            _ => IsEnabled);
    }

    /// <summary>Gets the engine's identifier of the option, for example <c>mode</c> or <c>thinking</c>.</summary>
    public string OptionId { get; }

    /// <summary>Gets the command that changes the value. Its parameter is an <see cref="OptionChoice"/>; any other parameter does nothing.</summary>
    public AgentPanelCommand SelectCommand { get; }

    /// <summary>Gets the values the engine offers, in its order. Empty until the host reports them.</summary>
    public IReadOnlyList<OptionChoice> Choices => _choices;

    /// <summary>Gets the value the option has now, or <see langword="null"/> when it is not among <see cref="Choices"/>.</summary>
    public OptionChoice? SelectedChoice => _selected;

    /// <summary>Gets the engine's identifier of the value in use, or <see langword="null"/> when the host reported none.</summary>
    public string? CurrentValue => _currentValue;

    /// <summary>Gets the text of the chip: the display name of the value in use, the raw identifier when the engine runs a value it does not list, or the option's own name before the host reported a value.</summary>
    public string Label => _selected?.Name ?? _currentValue ?? _displayName;

    /// <summary>Gets the plain-language description of the value in use, or <see langword="null"/> when the engine gave none.</summary>
    public string? CurrentDescription => _selected?.Description;

    /// <summary>Gets a value indicating whether the chip is shown: never in a terminal session, and only while the engine offers values to choose from.</summary>
    public bool IsVisible => !_isTerminalSession && _choices.Count > 0;

    /// <summary>Gets a value indicating whether the user can open the list and change the value: not while a run is active or a change is in flight, and not when the chip is hidden.</summary>
    public bool IsEnabled => IsVisible && !_isRunActive && !_isChanging;

    /// <summary>
    /// Gets the sentence for the chip's tooltip and help text: the reason the chip is disabled, or the value in use with the engine's own
    /// description of it (for the plan mode, what planning does) or, when the engine gave none, what the chip does.
    /// </summary>
    public string ToolTipText
    {
        get
        {
            if (_isRunActive)
            {
                return RunningReason;
            }

            if (_isChanging)
            {
                return $"Changing {_displayName.ToLowerInvariant()}.";
            }

            return CurrentDescription is { } description
                ? $"{_displayName}: {Label}. {description}"
                : $"{_displayName}: {Label}. Choose another.";
        }
    }

    /// <summary>Gets or sets whether a run is active. A run keeps the option fixed, so the chip is disabled while it is.</summary>
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

    /// <summary>Gets or sets whether the session is an omp terminal. A terminal runs its own omp, which owns its settings, so the chip is hidden.</summary>
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

    /// <summary>Gets a value indicating whether a change is waiting for the engine.</summary>
    public bool IsChanging
    {
        get => _isChanging;
        private set
        {
            if (SetProperty(ref _isChanging, value))
            {
                NotifyEnablementChanged();
            }
        }
    }

    /// <summary>Gets the sentence that says why the last change did not happen, or <see langword="null"/>. A new attempt or a change that succeeds clears it.</summary>
    public string? Notice
    {
        get => _notice;
        private set
        {
            if (SetProperty(ref _notice, value))
            {
                NotifyPropertyChanged(nameof(HasNotice));
            }
        }
    }

    /// <summary>Gets a value indicating whether <see cref="Notice"/> has text.</summary>
    public bool HasNotice => _notice is not null;

    /// <summary>
    /// Replaces the values and the value in use with what the host reports. The label follows a change the engine made on its own. The notice stays.
    /// </summary>
    /// <param name="choices">The values the engine offers; empty when it offers none.</param>
    /// <param name="currentValue">The identifier of the value in use, or null when none is reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="choices"/> is null.</exception>
    public void Refresh(IReadOnlyList<OptionChoice> choices, string? currentValue)
    {
        ArgumentNullException.ThrowIfNull(choices);

        _choices = choices;
        _currentValue = currentValue;
        _selected = FindSelected();
        NotifyPropertyChanged(nameof(Choices));
        NotifyCurrentChanged();
        NotifyPropertyChanged(nameof(IsVisible));
        NotifyEnablementChanged();
    }

    /// <summary>Forgets the values, as when the engine session behind the chip ended. The chip hides until the host reports values again.</summary>
    public void Reset() => Refresh([], null);

    /// <summary>
    /// Changes the option to <paramref name="choice"/> through the host. Does nothing when the chip is disabled, or the value is already in use.
    /// A refused or failed change leaves the value as it was and sets <see cref="Notice"/>.
    /// </summary>
    /// <param name="choice">The value to change to.</param>
    /// <returns>A task that completes when the host has answered. A fault of the host is thrown to the caller; <see cref="SelectCommand"/> turns it into the notice.</returns>
    public async Task SelectAsync(OptionChoice? choice)
    {
        if (choice is null || !IsEnabled)
        {
            return;
        }

        if (string.Equals(choice.Value, _currentValue, StringComparison.Ordinal))
        {
            Notice = null;
            return;
        }

        IsChanging = true;
        Notice = null;
        try
        {
            var result = await _setValue(choice.Value, CancellationToken.None).ConfigureAwait(true);
            if (result.IsSet)
            {
                SetCurrent(result.CurrentValue ?? choice.Value);
                return;
            }

            // The selection returns to the previous value: the engine's own answer wins when it names the value still in use.
            if (result.CurrentValue is { } stillInUse)
            {
                SetCurrent(stillInUse);
            }

            Notice = result.Message ?? $"{_displayName} could not be changed.";
        }
        finally
        {
            IsChanging = false;
        }
    }

    private OptionChoice? FindSelected()
    {
        foreach (var choice in _choices)
        {
            if (string.Equals(choice.Value, _currentValue, StringComparison.Ordinal))
            {
                return choice;
            }
        }

        return null;
    }

    private void SetCurrent(string value)
    {
        _currentValue = value;
        _selected = FindSelected();
        NotifyCurrentChanged();
    }

    private void NotifyCurrentChanged()
    {
        NotifyPropertyChanged(nameof(CurrentValue));
        NotifyPropertyChanged(nameof(SelectedChoice));
        NotifyPropertyChanged(nameof(Label));
        NotifyPropertyChanged(nameof(CurrentDescription));
        NotifyPropertyChanged(nameof(ToolTipText));
    }

    private void NotifyEnablementChanged()
    {
        NotifyPropertyChanged(nameof(IsEnabled));
        NotifyPropertyChanged(nameof(ToolTipText));
        SelectCommand.NotifyCanExecuteChanged();
    }
}
