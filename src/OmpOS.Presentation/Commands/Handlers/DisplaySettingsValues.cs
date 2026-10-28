namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>The values of the display switches.</summary>
/// <param name="VimMode">Vim keys in the composer. Off by default, as in Cursor.</param>
/// <param name="ShowLineNumbers">Line numbers in code blocks. On by default, so a code block reads as it does today.</param>
/// <param name="ShowThinkingBlocks">Thinking blocks in the transcript. On by default.</param>
/// <param name="ShowStatusIndicators">Status indicators in the window. On by default.</param>
/// <param name="ShowStatusLineRunningTime">The running time on the status line. On by default.</param>
/// <param name="RewindEnabled">Whether the rewind commands work. On by default, as in Cursor.</param>
public sealed record DisplaySettingsValues(bool VimMode, bool ShowLineNumbers, bool ShowThinkingBlocks, bool ShowStatusIndicators, bool ShowStatusLineRunningTime, bool RewindEnabled)
{
    /// <summary>Gets the values a fresh install has.</summary>
    public static DisplaySettingsValues Defaults { get; } = new(false, true, true, true, true, true);

    /// <summary>Reads one switch.</summary>
    /// <param name="setting">The switch.</param>
    /// <returns>Its value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="setting"/> is not a defined value.</exception>
    public bool Get(DisplaySetting setting) => setting switch
    {
        DisplaySetting.VimMode => VimMode,
        DisplaySetting.LineNumbers => ShowLineNumbers,
        DisplaySetting.ThinkingBlocks => ShowThinkingBlocks,
        DisplaySetting.StatusIndicators => ShowStatusIndicators,
        DisplaySetting.StatusLineRunningTime => ShowStatusLineRunningTime,
        DisplaySetting.Rewind => RewindEnabled,
        _ => throw new ArgumentOutOfRangeException(nameof(setting)),
    };

    /// <summary>Gets the same values with one switch changed.</summary>
    /// <param name="setting">The switch.</param>
    /// <param name="value">The new value.</param>
    /// <returns>The new values.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="setting"/> is not a defined value.</exception>
    public DisplaySettingsValues With(DisplaySetting setting, bool value) => setting switch
    {
        DisplaySetting.VimMode => this with { VimMode = value },
        DisplaySetting.LineNumbers => this with { ShowLineNumbers = value },
        DisplaySetting.ThinkingBlocks => this with { ShowThinkingBlocks = value },
        DisplaySetting.StatusIndicators => this with { ShowStatusIndicators = value },
        DisplaySetting.StatusLineRunningTime => this with { ShowStatusLineRunningTime = value },
        DisplaySetting.Rewind => this with { RewindEnabled = value },
        _ => throw new ArgumentOutOfRangeException(nameof(setting)),
    };
}
