using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Composer.Attachments;
using Autonogrammer.Presentation.Mentions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Composer;

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
    private MentionComposerModel? _mentions;
    private ContextRingModel? _contextRing;
    private Func<string, IReadOnlyList<MentionAttachment>, Task<bool>>? _mentionSender;
    private int _caretIndex;
    private ComposerAttachmentsModel? _images;
    private Func<string, IReadOnlyList<MentionAttachment>, IReadOnlyList<ComposerAttachment>, Task<bool>>? _imageSender;

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
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _draft, value))
            {
                if (_caretIndex > value.Length)
                {
                    _caretIndex = value.Length;
                    NotifyPropertyChanged(nameof(CaretIndex));
                }

                _slashPalette?.UpdateDraft(value);
                _mentions?.UpdateDraft(value, _caretIndex);
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

    /// <summary>
    /// Gets or sets the mention state of the session (the picker, the chips and the context estimate), or null when the session offers no @ mentions (an omp terminal).
    /// While it is set, every change of <see cref="Draft"/> and <see cref="CaretIndex"/> is reported to it, so the picker opens while the caret is in an <c>@</c> or <c>#</c> token, and
    /// the estimate of the attached items feeds <see cref="ContextRing"/>. The owner of the model disposes it.
    /// </summary>
    public MentionComposerModel? Mentions
    {
        get => _mentions;
        set
        {
            if (ReferenceEquals(_mentions, value))
            {
                return;
            }

            if (_mentions is not null)
            {
                _mentions.PropertyChanged -= OnMentionsChanged;
            }

            _mentions = value;
            if (_mentions is not null)
            {
                _mentions.PropertyChanged += OnMentionsChanged;
                _mentions.UpdateDraft(_draft, Math.Min(_caretIndex, _draft.Length));
            }

            NotifyPropertyChanged();
            _contextRing?.SetPending(_mentions?.Budget.EstimatedTokens ?? 0);
        }
    }

    /// <summary>
    /// Gets or sets the context ring of the session: the window usage the engine reported, shown beside the chips. The composer keeps its pending estimate equal to the
    /// estimate of the attached items. It is null when the session reports no usage.
    /// </summary>
    public ContextRingModel? ContextRing
    {
        get => _contextRing;
        set
        {
            if (SetProperty(ref _contextRing, value))
            {
                value?.SetPending(_mentions?.Budget.EstimatedTokens ?? 0);
            }
        }
    }

    /// <summary>Gets or sets the attached images of the message, or null when the session offers no image input. The owner of the model feeds it the capabilities of the engine and the model.</summary>
    public ComposerAttachmentsModel? Images
    {
        get => _images;
        set => SetProperty(ref _images, value);
    }

    /// <summary>
    /// Gets or sets the send path of a message that carries images: it receives the text, the content of each attached mention (possibly none) and the images, and resolves to true when the host
    /// accepted the send. With no sender, a message that carries images is refused (the draft and the images stay) so an image is never dropped silently.
    /// </summary>
    public Func<string, IReadOnlyList<MentionAttachment>, IReadOnlyList<ComposerAttachment>, Task<bool>>? ImageSender
    {
        get => _imageSender;
        set => _imageSender = value;
    }

    /// <summary>
    /// Gets or sets the send path of a message that carries mentions: it receives the text and the content read for each attached item, and resolves to true when the host accepted the
    /// send. With no sender, a message that carries mentions is sent as its text alone, through the callback given to the constructor.
    /// </summary>
    public Func<string, IReadOnlyList<MentionAttachment>, Task<bool>>? MentionSender
    {
        get => _mentionSender;
        set => _mentionSender = value;
    }

    /// <summary>
    /// Gets or sets the caret index in <see cref="Draft"/>. The head reports it as the caret moves; the mention picker opens while the caret is inside an <c>@</c> or <c>#</c> token. It is
    /// clamped to the draft. The composer also sets it, after a mention replaced the typed token, and the head moves its caret to it.
    /// </summary>
    public int CaretIndex
    {
        get => _caretIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, _draft.Length);
            if (SetProperty(ref _caretIndex, clamped))
            {
                _mentions?.UpdateDraft(_draft, clamped);
            }
        }
    }

    /// <summary>Gets whether the mention picker is open.</summary>
    public bool IsMentionPickerOpen => _mentions is { Picker.IsOpen: true };

    /// <summary>
    /// Handles a key while the mention picker is open. Down and Up move the highlight, Enter and Tab attach the highlighted item (they replace the typed token with the mention
    /// and add a chip; they do not send), and Escape closes the picker. Enter with no row listed is not handled, so the draft is sent as written. Nothing is handled while the picker is closed.
    /// </summary>
    /// <param name="key">The key name as the browser reports it, for example <c>ArrowDown</c>, <c>Enter</c>, <c>Tab</c> or <c>Escape</c>.</param>
    /// <returns><see langword="true"/> when the picker took the key, so the caller must not act on it again.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public async Task<bool> HandleMentionPickerKeyAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_mentions is not { Picker.IsOpen: true } mentions)
        {
            return false;
        }

        var picker = mentions.Picker;
        switch (key)
        {
            case "ArrowDown" when picker.Rows.Count > 0:
                picker.MoveSelection(1);
                return true;
            case "ArrowUp" when picker.Rows.Count > 0:
                picker.MoveSelection(-1);
                return true;
            case "Enter" or "Tab" when picker.SelectedRow is not null:
                await AcceptMentionAsync().ConfigureAwait(true);
                return true;
            case "Escape":
                picker.Dismiss();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Attaches the picker's highlighted item: the typed token in the draft becomes the mention's text, a chip is added and the caret moves after the text. A file that may hold secrets
    /// is not attached the first time: the picker asks the user to confirm, and the next call attaches it. Nothing is changed when the user typed on while the item was being resolved.
    /// </summary>
    /// <returns><see langword="true"/> when the draft was changed.</returns>
    public async Task<bool> AcceptMentionAsync()
    {
        if (_mentions is null)
        {
            return false;
        }

        var before = _draft;
        var edit = await _mentions.AcceptSelectedAsync(before, CancellationToken.None).ConfigureAwait(true);
        if (edit is null || !string.Equals(_draft, before, StringComparison.Ordinal))
        {
            return false;
        }

        // The caret first: the draft change reports the draft and the caret to the picker, which must see the caret after the inserted text so it closes.
        _caretIndex = Math.Clamp(edit.Caret, 0, edit.Draft.Length);
        Draft = edit.Draft;
        NotifyPropertyChanged(nameof(CaretIndex));
        return true;
    }

    /// <summary>Removes an attached item: its chip goes, and so does its text in the draft.</summary>
    /// <param name="chip">The chip to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chip"/> is null.</exception>
    public void RemoveMention(MentionChip chip)
    {
        ArgumentNullException.ThrowIfNull(chip);

        if (_mentions is null)
        {
            return;
        }

        var edit = _mentions.RemoveChip(chip, _draft);
        _caretIndex = Math.Clamp(edit.Caret, 0, edit.Draft.Length);
        Draft = edit.Draft;
        NotifyPropertyChanged(nameof(CaretIndex));
    }

    private void OnMentionsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MentionComposerModel.Budget), StringComparison.Ordinal))
        {
            _contextRing?.SetPending(_mentions?.Budget.EstimatedTokens ?? 0);
        }
    }

    /// <summary>Submits the draft unless it is blank, a send is already in flight, or a response is streaming.</summary>
    /// <returns>A task that completes when the host has answered the send.</returns>
    public async Task SendAsync()
    {
        var hasImages = _images is { HasItems: true };
        if (_sending || IsStreaming || (string.IsNullOrWhiteSpace(Draft) && !hasImages) || (hasImages && _imageSender is null))
        {
            return;
        }

        var text = Draft;
        _sending = true;
        try
        {
            // The content of each attached item is read now, so a diff or a terminal is as fresh as the send.
            IReadOnlyList<MentionAttachment> attachments = [];
            if (_mentions is { HasChips: true } mentions)
            {
                var collected = await mentions.CollectAsync(CancellationToken.None).ConfigureAwait(true);
                if (collected.Failures.Count > 0)
                {
                    // The unreadable items were dropped and named in the mentions' notice; the draft stays so the user can send again.
                    return;
                }

                attachments = collected.Attachments;
            }

            // Continue on the captured context: the code below writes Draft, which the UI binds to.
            bool accepted;
            if (_images is { HasItems: true } images && _imageSender is { } imageSender)
            {
                accepted = await imageSender(text, attachments, images.Items).ConfigureAwait(true);
            }
            else
            {
                accepted = attachments.Count > 0 && _mentionSender is { } sender
                    ? await sender(text, attachments).ConfigureAwait(true)
                    : await _onSend(text).ConfigureAwait(true);
            }

            // Clear only the message the host accepted. Text typed while the send was pending is not the sent text, so it stays.
            if (accepted && string.Equals(Draft, text, StringComparison.Ordinal))
            {
                Draft = string.Empty;
            }

            if (accepted)
            {
                _mentions?.Clear();
                _images?.Clear();
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
    /// Handles one key press in the draft. While the slash palette is open it takes the keys of <see cref="HandleSlashPaletteKey"/> first, then the mention picker takes the keys of
    /// <see cref="HandleMentionPickerKeyAsync"/>. Otherwise Enter without Shift sends and Escape stops while a response streams. Shift+Enter and every other key are left to the text control.
    /// </summary>
    /// <param name="key">The key name as the browser reports it, for example <c>Enter</c> or <c>Escape</c>.</param>
    /// <param name="shift">Whether Shift was held when the key was pressed.</param>
    /// <returns>A task that completes when the resulting send or stop has been handled.</returns>
    public async Task HandleKeyAsync(string key, bool shift)
    {
        if (!shift && HandleSlashPaletteKey(key))
        {
            return;
        }

        if (!shift && await HandleMentionPickerKeyAsync(key).ConfigureAwait(true))
        {
            return;
        }

        if (string.Equals(key, "Escape", StringComparison.Ordinal))
        {
            if (IsStreaming)
            {
                await StopAsync().ConfigureAwait(true);
            }

            return;
        }

        if (string.Equals(key, "Enter", StringComparison.Ordinal) && !shift)
        {
            await SendAsync().ConfigureAwait(true);
        }
    }
}
