namespace Autonogrammer.Presentation.Composer;

/// <summary>
/// A chip of the composer for one select-style option of the session that the engine owns, for example the mode (<c>default</c> or
/// <c>plan</c>) or the thinking level. The engine owns the list of values and the current value: the chip shows what the host reports
/// through <see cref="Refresh"/> and asks the host to change the value through the delegate it was built with. The chip is enabled only
/// while no run is active; a refused or failed change leaves the value as it was and sets <c>Notice</c>.
/// </summary>
/// <remarks>The chip runs on the UI context that owns the other presentation models and continues on it after each await.</remarks>
public sealed class OptionChipModel : EngineSelectModel<OptionChoice, OptionChangeResult>
{
    private const string RunningReason = "This setting cannot be changed while a response is running.";

    private readonly string _displayName;

    /// <summary>Creates a chip that knows no value yet.</summary>
    /// <param name="optionId">The engine's identifier of the option, for example <c>mode</c>.</param>
    /// <param name="displayName">The name of the option as the chip's tooltip says it, for example <c>Mode</c>.</param>
    /// <param name="setValue">Asks the host to give the option a <see cref="OptionChoice.Value"/>. It answers whether the engine confirmed the change.</param>
    /// <exception cref="ArgumentException"><paramref name="optionId"/> or <paramref name="displayName"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="setValue"/> is null.</exception>
    public OptionChipModel(string optionId, string displayName, Func<string, CancellationToken, Task<OptionChangeResult>> setValue)
        : base(displayName, setValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);

        OptionId = optionId;
        _displayName = displayName;
    }

    /// <summary>Gets the engine's identifier of the option, for example <c>mode</c> or <c>thinking</c>.</summary>
    public string OptionId { get; }

    /// <summary>Gets a value indicating whether a change is waiting for the engine.</summary>
    public bool IsChanging => IsInFlight;

    /// <summary>Gets the plain-language description of the value in use, or <see langword="null"/> when the engine gave none.</summary>
    public string? CurrentDescription => SelectedChoice?.Description;

    /// <summary>Gets the text of the chip: the display name of the value in use, the raw identifier when the engine runs a value it does not list, or the option's own name before the host reported a value.</summary>
    public override string Label => SelectedChoice?.Name ?? CurrentValue ?? _displayName;

    /// <summary>Gets a value indicating whether the chip is shown: never in a terminal session, and only while the engine offers values to choose from.</summary>
    public override bool IsVisible => !IsTerminalSession && Choices.Count > 0;

    /// <summary>
    /// Gets the sentence for the chip's tooltip and help text: the reason the chip is disabled, or the value in use with the engine's own
    /// description of it (for the plan mode, what planning does) or, when the engine gave none, what the chip does.
    /// </summary>
    public override string ToolTipText
    {
        get
        {
            if (IsRunActive)
            {
                return RunningReason;
            }

            if (IsChanging)
            {
                return $"Changing the {_displayName} option.";
            }

            return CurrentDescription is { } description
                ? $"{_displayName}: {Label}. {description}"
                : $"{_displayName}: {Label}. Choose another.";
        }
    }

    /// <summary>
    /// Replaces the values and the value in use with what the host reports. The label follows a change the engine made on its own. The notice stays.
    /// </summary>
    /// <param name="choices">The values the engine offers; empty when it offers none.</param>
    /// <param name="currentValue">The identifier of the value in use, or null when none is reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="choices"/> is null.</exception>
    public void Refresh(IReadOnlyList<OptionChoice> choices, string? currentValue)
    {
        ReplaceChoices(choices, currentValue);
        NotifyPropertyChanged(nameof(IsVisible));
        NotifyEnablementChanged();
    }

    /// <summary>Forgets the values, as when the engine session behind the chip ended. The chip hides until the host reports values again.</summary>
    public void Reset() => Refresh([], null);

    /// <inheritdoc/>
    protected override string ValueOf(OptionChoice choice) => choice.Value;

    /// <inheritdoc/>
    protected override (bool Confirmed, string? CurrentValue, string? Message) ReadResult(OptionChangeResult result) =>
        (result.IsSet, result.CurrentValue, result.Message);

    /// <inheritdoc/>
    protected override void NotifyCurrentChanged()
    {
        base.NotifyCurrentChanged();
        NotifyPropertyChanged(nameof(CurrentDescription));
    }

    /// <inheritdoc/>
    protected override void OnInFlightChanged()
    {
        NotifyPropertyChanged(nameof(IsChanging));
        base.OnInFlightChanged();
    }
}
