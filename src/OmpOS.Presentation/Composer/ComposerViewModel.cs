using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Composer;

/// <summary>
/// The draft, send gating and key handling behind the composer. Keys arrive as primitives, so the rules hold
/// with no UI framework; the Razor shell reads the browser key event and passes its name and Shift state.
/// </summary>
/// <remarks>
/// A send still in flight, or one made while a response streams, leaves the draft in place. The draft clears
/// only when the host accepts the text that was sent, so text typed while the send was pending stays.
/// </remarks>
public sealed class ComposerViewModel : ObservableModel
{
    private readonly Func<string, Task<bool>> _onSend;
    private readonly Func<Task> _onStop;
    private bool _sending;
    private string _draft = string.Empty;
    private ModelPickerModel? _modelPicker;
    private OptionChipModel? _modeChip;
    private OptionChipModel? _thinkingChip;
    private SlashPaletteModel? _slashPalette;
    private bool _isStreaming;

    /// <summary>Creates a composer that submits drafts through <paramref name="onSend"/> and stops responses through <paramref name="onStop"/>.</summary>
    /// <param name="onSend">Submits the draft. Resolves to true when the host accepted the send and to false when it refused it.</param>
    /// <param name="onStop">Stops the streaming response.</param>
    /// <exception cref="ArgumentNullException">A callback is null.</exception>
    public ComposerViewModel(Func<string, Task<bool>> onSend, Func<Task> onStop)
    {
        ArgumentNullException.ThrowIfNull(onSend);
        ArgumentNullException.ThrowIfNull(onStop);

        _onSend = onSend;
        _onStop = onStop;
    }

    /// <summary>Gets or sets the draft text the user is composing.</summary>
    public string Draft
    {
        get => _draft;
        set
        {
            if (SetProperty(ref _draft, value))
            {
                _slashPalette?.UpdateDraft(value);
            }
        }
    }

    /// <summary>Gets or sets whether the assistant is streaming. Escape stops only while it is, and the stop control replaces send.</summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        set
        {
            if (SetProperty(ref _isStreaming, value))
            {
                // A run keeps the model, the mode and the thinking level fixed.
                if (_modelPicker is not null)
                {
                    _modelPicker.IsRunActive = value;
                }

                if (_modeChip is not null)
                {
                    _modeChip.IsRunActive = value;
                }

                if (_thinkingChip is not null)
                {
                    _thinkingChip.IsRunActive = value;
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the model picker of the session, or null when the session offers no model control (no engine composed, or a terminal).
    /// While it is set, it follows <see cref="IsStreaming"/>: the model cannot be changed while a response streams.
    /// </summary>
    public ModelPickerModel? ModelPicker
    {
        get => _modelPicker;
        set
        {
            if (SetProperty(ref _modelPicker, value) && value is not null)
            {
                value.IsRunActive = _isStreaming;
            }
        }
    }

    /// <summary>
    /// Gets or sets the chip of the session's mode (<c>default</c> or <c>plan</c>), or null when omp offers no mode option or the session is
    /// an omp terminal. While it is set, it follows <see cref="IsStreaming"/>: the mode cannot be changed while a response streams.
    /// </summary>
    public OptionChipModel? ModeChip
    {
        get => _modeChip;
        set
        {
            if (SetProperty(ref _modeChip, value) && value is not null)
            {
                value.IsRunActive = _isStreaming;
            }
        }
    }

    /// <summary>
    /// Gets or sets the chip of the session's thinking level, or null when omp offers no thinking option or the session is an omp
    /// terminal. While it is set, it follows <see cref="IsStreaming"/>: the level cannot be changed while a response streams.
    /// </summary>
    public OptionChipModel? ThinkingChip
    {
        get => _thinkingChip;
        set
        {
            if (SetProperty(ref _thinkingChip, value) && value is not null)
            {
                value.IsRunActive = _isStreaming;
            }
        }
    }

    /// <summary>
    /// Gets or sets the slash palette of the session, or null when the session offers none (an omp terminal, or a session whose engine cannot be
    /// asked for its commands). While it is set, every change of <see cref="Draft"/> is reported to it, so it opens while the draft starts with a
    /// slash and a command name is being typed. The palette only lists and completes names: omp runs a slash command when the prompt that starts
    /// with it is sent, and a name typed by hand is sent exactly as written.
    /// </summary>
    public SlashPaletteModel? SlashPalette
    {
        get => _slashPalette;
        set
        {
            if (SetProperty(ref _slashPalette, value))
            {
                value?.UpdateDraft(_draft);
            }
        }
    }

    /// <summary>
    /// Handles a key while the slash palette is open. Down and Up move the highlight, Enter and Tab accept the highlighted command (they put
    /// <c>/name </c> in the draft and do not send), and Escape closes the palette. Enter with no command listed is not handled, so the draft is sent
    /// as it was written. Nothing is handled while the palette is closed.
    /// </summary>
    /// <param name="key">The key name as the browser reports it, for example <c>ArrowDown</c>, <c>Enter</c>, <c>Tab</c> or <c>Escape</c>.</param>
    /// <returns><see langword="true"/> when the palette took the key, so the caller must not act on it again.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public bool HandleSlashPaletteKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_slashPalette is not { IsOpen: true } palette)
        {
            return false;
        }

        switch (key)
        {
            case "ArrowDown" when palette.Entries.Count > 0:
                palette.MoveSelection(1);
                return true;
            case "ArrowUp" when palette.Entries.Count > 0:
                palette.MoveSelection(-1);
                return true;
            case "Enter" or "Tab" when palette.SelectedEntry is not null:
                AcceptSlashEntry();
                return true;
            case "Escape":
                palette.Dismiss();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Puts the highlighted command of the slash palette in the draft: the draft becomes <c>/name </c>, ready for the command's input. It sends
    /// nothing and runs nothing. A disabled command is not put in the draft; the palette shows its reason.
    /// </summary>
    /// <returns><see langword="true"/> when the draft was replaced.</returns>
    public bool AcceptSlashEntry()
    {
        if (_slashPalette?.Accept() is not { } text)
        {
            return false;
        }

        Draft = text;
        return true;
    }

    /// <summary>Submits the draft unless it is blank, a send is already in flight, or a response is streaming.</summary>
    /// <returns>A task that completes when the host has answered the send.</returns>
    public async Task SendAsync()
    {
        if (_sending || IsStreaming || string.IsNullOrWhiteSpace(Draft))
        {
            return;
        }

        var text = Draft;
        _sending = true;
        try
        {
            // Continue on the captured context: the code below writes Draft, which the UI binds to.
            var accepted = await _onSend(text).ConfigureAwait(true);

            // Clear only the message the host accepted. Text typed while the send was pending is not the sent text, so it stays.
            if (accepted && string.Equals(Draft, text, StringComparison.Ordinal))
            {
                Draft = string.Empty;
            }
        }
        finally
        {
            _sending = false;
        }
    }

    /// <summary>Stops the streaming response.</summary>
    /// <returns>A task that completes when the host has handled the stop.</returns>
    public Task StopAsync() => _onStop();

    /// <summary>
    /// Handles one key press in the draft. While the slash palette is open it takes the keys of <see cref="HandleSlashPaletteKey"/> first. Otherwise
    /// Enter without Shift sends and Escape stops while a response streams. Shift+Enter and every other key are left to the text control.
    /// </summary>
    /// <param name="key">The key name as the browser reports it, for example <c>Enter</c> or <c>Escape</c>.</param>
    /// <param name="shift">Whether Shift was held when the key was pressed.</param>
    /// <returns>A task that completes when the resulting send or stop has been handled.</returns>
    public Task HandleKeyAsync(string key, bool shift)
    {
        if (!shift && HandleSlashPaletteKey(key))
        {
            return Task.CompletedTask;
        }

        if (string.Equals(key, "Escape", StringComparison.Ordinal))
        {
            return IsStreaming ? StopAsync() : Task.CompletedTask;
        }

        if (string.Equals(key, "Enter", StringComparison.Ordinal) && !shift)
        {
            return SendAsync();
        }

        return Task.CompletedTask;
    }
}
