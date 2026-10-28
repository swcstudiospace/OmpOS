using OmpOS.Presentation.Commands.Handlers;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// The Display page: vim mode, line numbers, thinking blocks, status indicators, running time and rewind. The switches are a draft until
/// <see cref="EditableSettingsPage{TValues}.SaveAsync"/> writes the ones that changed through <see cref="DisplaySettings"/>, the object the
/// slash commands and the views read, so a saved switch is in force at once. A file that cannot be written is an inline save error.
/// </summary>
public sealed class DisplaySettingsPage : EditableSettingsPage<DisplaySettingsValues>
{
    private static readonly DisplayToggle[] AllToggles =
    [
        new(DisplaySetting.VimMode, DisplayCopy.VimLabel, DisplayCopy.VimHelp),
        new(DisplaySetting.LineNumbers, DisplayCopy.LineNumbersLabel, DisplayCopy.LineNumbersHelp),
        new(DisplaySetting.ThinkingBlocks, DisplayCopy.ThinkingLabel, DisplayCopy.ThinkingHelp),
        new(DisplaySetting.StatusIndicators, DisplayCopy.StatusIndicatorsLabel, DisplayCopy.StatusIndicatorsHelp),
        new(DisplaySetting.StatusLineRunningTime, DisplayCopy.RunningTimeLabel, DisplayCopy.RunningTimeHelp),
        new(DisplaySetting.Rewind, DisplayCopy.RewindLabel, DisplayCopy.RewindHelp),
    ];

    private readonly DisplaySettings _display;

    /// <summary>Initializes a new instance of the <see cref="DisplaySettingsPage"/> class.</summary>
    /// <param name="display">The display settings the head shares with the views and the slash commands.</param>
    /// <exception cref="ArgumentNullException"><paramref name="display"/> is null.</exception>
    public DisplaySettingsPage(DisplaySettings display)
        : base(SettingsSection.Display, DisplayCopy.Title, DisplaySettingsValues.Defaults)
    {
        ArgumentNullException.ThrowIfNull(display);
        _display = display;
    }

    /// <summary>Gets the switches in display order.</summary>
    public static IReadOnlyList<DisplayToggle> Toggles => AllToggles;

    /// <summary>Returns the draft value of a switch.</summary>
    /// <param name="setting">The setting.</param>
    /// <returns>True when the draft turns the setting on.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="setting"/> is not defined.</exception>
    public bool IsOn(DisplaySetting setting) => Draft.Get(setting);

    /// <summary>Sets a switch in the draft. Ignored while the page is not ready.</summary>
    /// <param name="setting">The setting.</param>
    /// <param name="value">True to turn it on.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="setting"/> is not defined.</exception>
    public void SetToggle(DisplaySetting setting, bool value) => SetDraft(Draft.With(setting, value));

    /// <inheritdoc/>
    protected override async Task<DisplaySettingsValues> LoadValuesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _display.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SettingsSourceException(DisplayCopy.LoadFailed, exception);
        }

        return _display.Values;
    }

    /// <inheritdoc/>
    protected override async Task SaveValuesAsync(DisplaySettingsValues values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        try
        {
            foreach (var setting in Enum.GetValues<DisplaySetting>())
            {
                if (values.Get(setting) != _display.Get(setting))
                {
                    await _display.SetAsync(setting, values.Get(setting), cancellationToken).ConfigureAwait(true);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SettingsSourceException(DisplayCopy.SaveFailed, exception);
        }
    }

    /// <inheritdoc/>
    protected override void NotifyValueProperties() => NotifyPropertyChanged(nameof(Toggles));
}
