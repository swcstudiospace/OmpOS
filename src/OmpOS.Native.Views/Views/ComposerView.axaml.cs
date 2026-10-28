using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmpOS.Harness.Completion;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Mentions;
using OmpOS.Presentation.Runs;

namespace OmpOS.Native.Views;

/// <summary>
/// The composer card: the prompt input, the model, mode and thinking chips, and the send, follow-up and stop controls. The draft, send gating and
/// key rules live in the bound <see cref="ComposerViewModel"/>; this view forwards key presses and clicks to it and keeps the
/// prompt text equal to the draft. While <see cref="Runs"/> reports an active run, the send control steers that run and the
/// follow-up control queues the draft behind it, and a label states which kind of input is pending.
/// </summary>
internal partial class ComposerView : UserControl
{
    private const string SendName = "Send message";
    private const string SteerName = "Steer message";

    private int _inFlight;
    private RunInputScheduler? _runs;
    private SteeringInputModel? _steering;
    private ComposerViewModel? _composer;
    private MentionComposerModel? _mentionModel;
    private ContextRingModel? _ringModel;

    /// <summary>Creates the composer with no data context and no run input.</summary>
    public ComposerView()
    {
        InitializeComponent();
        PromptInput.PropertyChanged += OnPromptPropertyChanged;
        PromptInput.AddHandler(KeyDownEvent, OnPromptKeyDown, RoutingStrategies.Tunnel);
        SendButton.Click += OnSendClick;
        FollowUpButton.Click += OnFollowUpClick;
        StopButton.Click += OnStopClick;
        ModelPicker.FocusReturnRequested += OnModelPickerFocusReturn;
        ModeChipView.FocusReturnRequested += OnModelPickerFocusReturn;
        ThinkingChipView.FocusReturnRequested += OnModelPickerFocusReturn;
        SlashPalette.EntryChosen += OnSlashEntryChosen;
        MentionPicker.RowChosen += OnMentionRowChosen;
        MentionChips.AddHandler(Button.ClickEvent, OnChipClick);
        InitialiseAttachments();
        RefreshRunState();
    }

    /// <summary>Gets whether a key press or click has started work that has not settled yet.</summary>
    public bool IsWorking => _inFlight > 0;

    /// <summary>
    /// Gets or sets the completion client the inline suggestions box asks for suggestions. Null, or a client whose settings configure
    /// no provider, sends no request.
    /// </summary>
    public CompletionClient? Completion
    {
        get => Suggestions.Client;
        set => Suggestions.Client = value;
    }

    /// <summary>
    /// Gets or sets the callback that queues a message behind the active run when Tab is pressed in the prompt. It returns <see langword="true"/> when the message was queued (the draft then clears) and <see langword="false"/> when it was refused
    /// (the draft stays). Null leaves Tab to move focus.
    /// </summary>
    public Func<string, bool>? QueueRequest { get; set; }

    /// <summary>
    /// Gets or sets the run input of the active run. Null means no run input is connected: the send control then always sends
    /// a new prompt, and no steer or follow-up control is offered.
    /// </summary>
    public RunInputScheduler? Runs
    {
        get => _runs;
        set
        {
            if (ReferenceEquals(_runs, value))
            {
                return;
            }

            if (_runs is not null)
            {
                _runs.PropertyChanged -= OnRunsChanged;
            }

            _runs = value;
            if (_runs is not null)
            {
                _runs.PropertyChanged += OnRunsChanged;
            }

            RefreshRunState();
        }
    }

    /// <summary>Gets or sets whether the inline suggestions box is shown below the prompt. It is hidden by default.</summary>
    public bool SuggestionsShown
    {
        get => Suggestions.IsVisible;
        set => Suggestions.IsVisible = value;
    }

    /// <summary>Raised on the UI thread once no started work is in flight and the prompt matches the draft.</summary>
    public event EventHandler? WorkSettled;

    /// <summary>Raised on the UI thread when the work that a key press or click started faults. The argument is the fault.</summary>
    public event EventHandler<ComposerWorkFaultedEventArgs>? WorkFaulted;

    /// <summary>
    /// Raised when Esc is pressed in the prompt while a run is active. While a handler is attached the composer does not stop the run: the
    /// handler opens the stop confirmation bar. Without a handler Esc stops the run at once.
    /// </summary>
    public event EventHandler? StopConfirmationRequested;

    /// <summary>Raised on the UI thread with a sentence for the session status line, for example that the queue is full.</summary>
    public event EventHandler<ComposerStatusEventArgs>? StatusMessage;

    /// <summary>
    /// Gets or sets the steering model of the session. While it is set, a steer or follow-up goes through it, so the message reaches the
    /// steering history and is recorded, and a refused message raises <see cref="StatusMessage"/> and keeps the draft.
    /// </summary>
    public SteeringInputModel? Steering
    {
        get => _steering;
        set
        {
            _steering = value;
            RefreshRunState();
        }
    }

    /// <summary>
    /// Gets or sets whether the hint line under the card is shown. It is hidden by default: the keyboard hint is the tooltip of the prompt
    /// and stays in its help text, so the line only shows where a shell asks for it.
    /// </summary>
    public bool HintVisible
    {
        get => HintLine.IsVisible;
        set => HintLine.IsVisible = value;
    }

    /// <summary>Moves keyboard focus to the prompt.</summary>
    public void FocusPrompt() => PromptInput.FocusWhenReady();

    /// <summary>Gets the model chip of the footer. It is hidden until the composer carries a model picker that has something to offer.</summary>
    internal ModelPickerView ModelChip => ModelPicker;

    /// <summary>Gets the mode chip of the footer (Default or Plan). It is hidden until the composer carries a mode chip that has values to offer.</summary>
    internal OptionChipView ModeChipControl => ModeChipView;

    /// <summary>Gets the thinking chip of the footer. It is hidden until the composer carries a thinking chip that has values to offer.</summary>
    internal OptionChipView ThinkingChipControl => ThinkingChipView;

    /// <summary>Gets the slash palette above the card. It is hidden until the draft starts with a slash and the composer carries a palette.</summary>
    internal SlashPaletteView SlashPaletteControl => SlashPalette;

    /// <summary>Gets the mention picker above the card. It is hidden until the caret is in an @ or # token and the composer carries mentions.</summary>
    internal MentionPickerView MentionPickerControl => MentionPicker;

    /// <summary>Gets the list of chips of the attached mentions. It is hidden while nothing is attached.</summary>
    internal ItemsControl MentionChipsList => MentionChips;

    /// <summary>Gets the context ring beside the chips. It is hidden until the composer carries a ring that has usage to show.</summary>
    internal ContextRingView ContextRingControl => ContextRing;

    /// <summary>Gets the row above the card that holds the chips and the context ring. It is hidden while there is neither.</summary>
    internal Grid ContextRowControl => ContextRow;

    /// <summary>Copies the draft into the prompt and binds the model, mode and thinking chips when the data context changes.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_composer is not null)
        {
            _composer.PropertyChanged -= OnComposerChanged;
        }

        _composer = DataContext as ComposerViewModel;
        if (_composer is not null)
        {
            _composer.PropertyChanged += OnComposerChanged;
            SyncPrompt(_composer);
        }

        ModelPicker.DataContext = _composer?.ModelPicker;
        ModeChipView.DataContext = _composer?.ModeChip;
        ThinkingChipView.DataContext = _composer?.ThinkingChip;
        SlashPalette.DataContext = _composer?.SlashPalette;
        BindMentions();
        BindAttachments();
    }

    private void OnComposerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ComposerViewModel.ModelPicker), StringComparison.Ordinal))
        {
            ModelPicker.DataContext = _composer?.ModelPicker;
        }
        else if (string.Equals(e.PropertyName, nameof(ComposerViewModel.ModeChip), StringComparison.Ordinal))
        {
            ModeChipView.DataContext = _composer?.ModeChip;
        }
        else if (string.Equals(e.PropertyName, nameof(ComposerViewModel.ThinkingChip), StringComparison.Ordinal))
        {
            ThinkingChipView.DataContext = _composer?.ThinkingChip;
        }
        else if (string.Equals(e.PropertyName, nameof(ComposerViewModel.SlashPalette), StringComparison.Ordinal))
        {
            SlashPalette.DataContext = _composer?.SlashPalette;
        }
        else if (string.Equals(e.PropertyName, nameof(ComposerViewModel.Mentions), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(ComposerViewModel.ContextRing), StringComparison.Ordinal))
        {
            BindMentions();
        }
        else if (string.Equals(e.PropertyName, nameof(ComposerViewModel.CaretIndex), StringComparison.Ordinal) && _composer is not null
            && PromptInput.CaretIndex != _composer.CaretIndex && _composer.CaretIndex <= (PromptInput.Text ?? string.Empty).Length)
        {
            // The composer moved the caret (a mention replaced the typed token): the text box follows.
            PromptInput.CaretIndex = _composer.CaretIndex;
        }
    }

    /// <summary>Binds the picker, the chips and the context ring to the mention state and the ring of the composer, and follows their changes.</summary>
    private void BindMentions()
    {
        var mentions = _composer?.Mentions;
        if (!ReferenceEquals(_mentionModel, mentions))
        {
            if (_mentionModel is not null)
            {
                _mentionModel.PropertyChanged -= OnMentionModelChanged;
            }

            _mentionModel = mentions;
            if (_mentionModel is not null)
            {
                _mentionModel.PropertyChanged += OnMentionModelChanged;
            }
        }

        var ring = _composer?.ContextRing;
        if (!ReferenceEquals(_ringModel, ring))
        {
            if (_ringModel is not null)
            {
                _ringModel.PropertyChanged -= OnRingModelChanged;
            }

            _ringModel = ring;
            if (_ringModel is not null)
            {
                _ringModel.PropertyChanged += OnRingModelChanged;
            }
        }

        MentionPicker.DataContext = mentions?.Picker;
        ContextRing.DataContext = ring;
        RefreshContextRow();
    }

    private void OnMentionModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MentionComposerModel.Chips), StringComparison.Ordinal))
        {
            RefreshContextRow();
        }
        else if (string.Equals(e.PropertyName, nameof(MentionComposerModel.Notice), StringComparison.Ordinal) && _mentionModel?.Notice is { } notice)
        {
            StatusMessage?.Invoke(this, new ComposerStatusEventArgs(notice));
        }
    }

    private void OnRingModelChanged(object? sender, PropertyChangedEventArgs e) => RefreshContextRow();

    private void RefreshContextRow()
    {
        var chips = _mentionModel?.Chips ?? [];
        if (!ReferenceEquals(MentionChips.ItemsSource, chips))
        {
            MentionChips.ItemsSource = chips;
        }

        MentionChips.IsVisible = chips.Count > 0;
        ContextRow.IsVisible = chips.Count > 0 || _ringModel is { Text.Length: > 0 };
    }

    private void OnMentionRowChosen(object? sender, EventArgs e)
    {
        if (DataContext is ComposerViewModel composer)
        {
            Track(composer, () => composer.AcceptMentionAsync());
        }

        FocusPrompt();
    }

    private void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control source && source.FindAncestorOfType<Button>(includeSelf: true) is { DataContext: MentionChip chip } && DataContext is ComposerViewModel composer)
        {
            composer.RemoveMention(chip);
            SyncPrompt(composer);
            FocusPrompt();
        }
    }

    /// <summary>
    /// Offers a key to the mention picker. The picker takes the arrow, Enter, Tab and Escape keys while it is open and has something to act on, before the send and stop rules.
    /// It attaches the highlighted item in the composer: Enter and Tab never send while the picker has a row selected.
    /// </summary>
    private bool TryHandleMentionPickerKey(ComposerViewModel composer, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || composer.Mentions is not { Picker: { IsOpen: true } picker } || SlashPaletteKeyName(e.Key) is not { } key)
        {
            return false;
        }

        var takes = key switch
        {
            "ArrowDown" or "ArrowUp" => picker.Rows.Count > 0,
            "Enter" or "Tab" => picker.SelectedRow is not null,
            "Escape" => true,
            _ => false,
        };
        if (!takes)
        {
            return false;
        }

        e.Handled = true;
        Track(composer, () => composer.HandleMentionPickerKeyAsync(key));
        return true;
    }

    private void OnModelPickerFocusReturn(object? sender, EventArgs e) => FocusPrompt();

    private bool RunIsActive => _runs is { IsRunActive: true };

    private void OnPromptPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (DataContext is not ComposerViewModel composer)
        {
            return;
        }

        if (e.Property == TextBox.TextProperty)
        {
            composer.Draft = PromptInput.Text ?? string.Empty;
        }
        else if (e.Property == TextBox.CaretIndexProperty)
        {
            composer.CaretIndex = PromptInput.CaretIndex;
        }
    }

    /// <summary>
    /// Offers a key to the slash palette. The palette takes the arrow, Enter, Tab and Escape keys while it is open, before the send and stop rules.
    /// It only completes a command name in the draft: omp runs the command when the prompt is sent.
    /// </summary>
    private bool TryHandleSlashPaletteKey(ComposerViewModel composer, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || SlashPaletteKeyName(e.Key) is not { } slashKey)
        {
            return false;
        }

        var before = composer.Draft;
        if (!composer.HandleSlashPaletteKey(slashKey))
        {
            return false;
        }

        e.Handled = true;
        if (!string.Equals(before, composer.Draft, StringComparison.Ordinal))
        {
            SyncPrompt(composer);
            PromptInput.CaretIndex = composer.Draft.Length;
        }

        return true;
    }

    // Tab queues the draft behind the active run (Cursor's rule); Enter keeps steering. A refused message stays in the box.
    private bool TryQueueDraft(ComposerViewModel composer, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || e.KeyModifiers != KeyModifiers.None || QueueRequest is not { } queue || _runs is not { IsRunActive: true } || string.IsNullOrWhiteSpace(composer.Draft))
        {
            return false;
        }

        e.Handled = true;
        if (queue(composer.Draft))
        {
            composer.Draft = string.Empty;
        }

        return true;
    }

    private void HandleEnter(ComposerViewModel composer, KeyEventArgs e, bool shift, bool ctrl)
    {
        if (shift && !ctrl)
        {
            // Shift+Enter is left alone, so the text box inserts the newline.
            return;
        }

        // Handled stops the text box from inserting the newline.
        e.Handled = true;
        if (ctrl && shift)
        {
            // Ctrl+Shift+Enter queues a follow-up while a run is active and is a no-op otherwise.
            if (_runs is { IsRunActive: true } active)
            {
                Track(composer, () => FollowUpAsync(composer, active));
            }
        }
        else if (ctrl)
        {
            // Ctrl+Enter steers while a run is active and sends otherwise.
            Track(composer, () => _runs is { IsRunActive: true } active ? SteerAsync(composer, active) : composer.SendAsync());
        }
        else
        {
            Track(composer, () => SubmitAsync(composer));
        }
    }

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ComposerViewModel composer)
        {
            return;
        }

        var shift = (e.KeyModifiers & KeyModifiers.Shift) != KeyModifiers.None;

        if (TryHandleSlashPaletteKey(composer, e))
        {
            return;
        }

        if (TryHandleMentionPickerKey(composer, e))
        {
            return;
        }

        if (TryQueueDraft(composer, e))
        {
            return;
        }

        var ctrl = (e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.None;
        if (e.Key == Key.Enter)
        {
            HandleEnter(composer, e, shift, ctrl);
        }
        else if (e.Key == Key.Escape)
        {
            if (RunIsActive && StopConfirmationRequested is { } requested)
            {
                // The shell asks first: the stop confirmation bar decides.
                e.Handled = true;
                requested(this, EventArgs.Empty);
                return;
            }

            Track(composer, () => composer.HandleKeyAsync("Escape", shift));
        }
    }

    private static string? SlashPaletteKeyName(Key key) => key switch
    {
        Key.Down => "ArrowDown",
        Key.Up => "ArrowUp",
        Key.Enter => "Enter",
        Key.Tab => "Tab",
        Key.Escape => "Escape",
        _ => null,
    };

    private void OnSlashEntryChosen(object? sender, EventArgs e)
    {
        if (DataContext is ComposerViewModel composer && composer.AcceptSlashEntry())
        {
            SyncPrompt(composer);
            PromptInput.CaretIndex = composer.Draft.Length;
        }

        FocusPrompt();
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ComposerViewModel composer)
        {
            Track(composer, () => SubmitAsync(composer));
        }
    }

    private void OnFollowUpClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ComposerViewModel composer && _runs is { IsRunActive: true } runs)
        {
            Track(composer, () => FollowUpAsync(composer, runs));
        }
    }

    private void OnStopClick(object? sender, RoutedEventArgs e) => StopResponse();

    /// <summary>
    /// Stops the streaming response through the composer's tracked work, so a stop that faults is reported on the status line. The composer's
    /// stop button and the transcript's stop control both stop this way.
    /// </summary>
    public void StopResponse()
    {
        if (DataContext is ComposerViewModel composer)
        {
            Track(composer, composer.StopAsync);
        }
    }

    private void OnRunsChanged(object? sender, PropertyChangedEventArgs e) => RefreshRunState();

    /// <summary>
    /// While a run is active the draft steers it, unless a follow-up already waits: then the draft queues behind that follow-up, so
    /// the messages start in the order they were sent. Otherwise the composer sends the draft as a new prompt.
    /// </summary>
    private Task SubmitAsync(ComposerViewModel composer)
    {
        if (_runs is not { IsRunActive: true } runs)
        {
            return composer.SendAsync();
        }

        // With a steering model the model keeps the send order itself: a steer queues behind waiting follow-ups.
        return _steering is null && runs.QueuedFollowUpCount > 0 ? FollowUpAsync(composer, runs) : SteerAsync(composer, runs);
    }

    /// <summary>Steers the active run with the draft, through the steering model when one is set so the entry reaches the history.</summary>
    private Task SteerAsync(ComposerViewModel composer, RunInputScheduler runs) =>
        _steering is { } steering ? SubmitSteeringAsync(composer, steering, SteeringMode.Steer) : SteerDraftAsync(composer, runs);

    /// <summary>Queues the draft as a follow-up behind the active run, through the steering model when one is set.</summary>
    private Task FollowUpAsync(ComposerViewModel composer, RunInputScheduler runs) =>
        _steering is { } steering ? SubmitSteeringAsync(composer, steering, SteeringMode.FollowUp) : QueueFollowUpAsync(composer, runs);

    private async Task SubmitSteeringAsync(ComposerViewModel composer, SteeringInputModel steering, SteeringMode mode)
    {
        var text = composer.Draft;
        var outcome = await steering.SubmitAsync(text, mode, CancellationToken.None).ConfigureAwait(true);
        switch (outcome)
        {
            case SteeringSubmitOutcome.Accepted:
                // Clear only the text that was accepted. Text typed while the message was in flight stays.
                if (string.Equals(composer.Draft, text, StringComparison.Ordinal))
                {
                    composer.Draft = string.Empty;
                }

                break;
            case SteeringSubmitOutcome.QueueFull:
                // The draft stays, so the user can send it again once the queue has room.
                StatusMessage?.Invoke(this, new ComposerStatusEventArgs(steering.RefusalText ?? AgentPanelCopy.QueueFull(RunInputScheduler.MaxQueuedMessages)));
                break;
            case SteeringSubmitOutcome.NoActiveRun:
                // The run ended while the key was pressed: the text is a new prompt.
                await composer.SendAsync().ConfigureAwait(true);
                break;
            default:
                break;
        }
    }

    private static async Task SteerDraftAsync(ComposerViewModel composer, RunInputScheduler runs)
    {
        var text = composer.Draft;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await runs.SteerAsync(text, CancellationToken.None).ConfigureAwait(true);

        // Clear only the text the run accepted. Text typed while the steer was in flight is not the sent text, so it stays.
        if (string.Equals(composer.Draft, text, StringComparison.Ordinal))
        {
            composer.Draft = string.Empty;
        }
    }

    private static Task QueueFollowUpAsync(ComposerViewModel composer, RunInputScheduler runs)
    {
        var text = composer.Draft;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        // The scheduler holds the follow-up until the active run completes. The draft clears because the message is now queued.
        try
        {
            runs.QueueFollowUp(text);
        }
        catch (InvalidOperationException refusal)
        {
            // A refused follow-up faults the returned task, so Track reports it on the status line and the draft stays.
            return Task.FromException(refusal);
        }

        composer.Draft = string.Empty;
        return Task.CompletedTask;
    }

    private void Track(ComposerViewModel composer, Func<Task> start)
    {
        _inFlight++;
        var work = start();

        // Faults are observed, not awaited: the fault is marshalled to the UI thread and reported there.
        _ = work.ContinueWith(
            faulted =>
            {
                if (faulted.Exception is { } fault)
                {
                    Dispatcher.UIThread.Post(() => WorkFaulted?.Invoke(this, new ComposerWorkFaultedEventArgs(fault)));
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        _ = work.ContinueWith(
            _ => Dispatcher.UIThread.Post(() =>
            {
                SyncPrompt(composer);
                _inFlight--;
                if (_inFlight == 0)
                {
                    WorkSettled?.Invoke(this, EventArgs.Empty);
                }
            }),
            TaskScheduler.Default);
    }

    private void SyncPrompt(ComposerViewModel composer)
    {
        var caret = composer.CaretIndex;
        if (!string.Equals(PromptInput.Text ?? string.Empty, composer.Draft, StringComparison.Ordinal))
        {
            PromptInput.Text = composer.Draft;

            // Setting the text moves the caret; the composer's caret is the one that counts, so it is put back.
            PromptInput.CaretIndex = Math.Min(caret, composer.Draft.Length);
        }
    }

    /// <summary>Shows the steer and follow-up controls and the pending label to match the run's state.</summary>
    private void RefreshRunState()
    {
        var active = RunIsActive;
        FollowUpButton.IsVisible = active;
        StopButton.Classes.Set("idle", !active);
        var live = active && _steering is { IsLive: true };
        AutomationProperties.SetName(SendButton, live ? AgentPanelCopy.ComposerSendNowName : active ? SteerName : SendName);
        ToolTip.SetTip(SendButton, live ? AgentPanelCopy.SendNow : active ? SteerName : SendName);
        AutomationProperties.SetName(FollowUpButton, live ? AgentPanelCopy.QueueFollowUpName : AgentPanelViewText.QueueFollowUpName);
        PromptInput.PlaceholderText = !active
            ? AgentPanelCopy.ComposerPlaceholderIdle
            : live ? AgentPanelCopy.ComposerPlaceholderRunningLive : AgentPanelCopy.ComposerPlaceholderRunning;

        var label = PendingLabelText();
        PendingLabel.Text = label;
        PendingLabel.IsVisible = label.Length > 0;
    }

    private string PendingLabelText()
    {
        if (_runs is null)
        {
            return string.Empty;
        }

        var held = AgentPanelCopy.HeldSteering(_runs.SteersHeld) ?? string.Empty;
        var pending = _runs.Pending switch
        {
            PendingRunInput.FollowUp => _runs.QueuedFollowUpCount == 1
                ? "1 follow-up is queued. It starts when the active run completes."
                : $"{_runs.QueuedFollowUpCount} follow-ups are queued. They start one at a time when the active run completes.",
            PendingRunInput.Steering => "A steering message is being delivered to the active run.",
            _ => _runs.IsRunActive
                ? _steering is { IsLive: true }
                    ? AgentPanelCopy.ComposerPendingActiveLive
                    : "A run is active. Send waits until the active turn ends; Follow-up waits until the run completes."
                : string.Empty,
        };

        if (held.Length == 0)
        {
            return pending;
        }

        return pending.Length == 0 ? held : $"{held} {pending}";
    }
}
