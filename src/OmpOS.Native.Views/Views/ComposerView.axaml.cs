using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmpOS.Harness.Completion;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Runs;

namespace OmpOS.Native.Views;

/// <summary>
/// The composer row: the prompt input, the send control, the follow-up control and the stop control. The draft, send gating and
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

    /// <summary>Creates the composer with no data context and no run input.</summary>
    public ComposerView()
    {
        InitializeComponent();
        PromptInput.PropertyChanged += OnPromptPropertyChanged;
        PromptInput.AddHandler(KeyDownEvent, OnPromptKeyDown, RoutingStrategies.Tunnel);
        SendButton.Click += OnSendClick;
        FollowUpButton.Click += OnFollowUpClick;
        StopButton.Click += OnStopClick;
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

    /// <summary>Copies the draft into the prompt when the data context changes.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ComposerViewModel composer)
        {
            SyncPrompt(composer);
        }
    }

    private bool RunIsActive => _runs is { IsRunActive: true };

    private void OnPromptPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && DataContext is ComposerViewModel composer)
        {
            composer.Draft = PromptInput.Text ?? string.Empty;
        }
    }

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ComposerViewModel composer)
        {
            return;
        }

        var shift = (e.KeyModifiers & KeyModifiers.Shift) != KeyModifiers.None;
        if (e.Key == Key.Enter && !shift)
        {
            // Handled stops the text box from inserting the newline. Shift+Enter is left alone, so it inserts one.
            e.Handled = true;
            Track(composer, () => SubmitAsync(composer));
        }
        else if (e.Key == Key.Escape)
        {
            Track(composer, () => composer.HandleKeyAsync("Escape", shift));
        }
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
            Track(composer, () => QueueFollowUpAsync(composer, runs));
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
    private Task SubmitAsync(ComposerViewModel composer) =>
        _runs is { IsRunActive: true } runs
            ? runs.QueuedFollowUpCount > 0 ? QueueFollowUpAsync(composer, runs) : SteerDraftAsync(composer, runs)
            : composer.SendAsync();

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
        runs.QueueFollowUp(text);
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
        if (!string.Equals(PromptInput.Text ?? string.Empty, composer.Draft, StringComparison.Ordinal))
        {
            PromptInput.Text = composer.Draft;
        }
    }

    /// <summary>Shows the steer and follow-up controls and the pending label to match the run's state.</summary>
    private void RefreshRunState()
    {
        var active = RunIsActive;
        FollowUpButton.IsVisible = active;
        AutomationProperties.SetName(SendButton, active ? SteerName : SendName);

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

        var held = _runs.SteersHeld switch
        {
            0 => string.Empty,
            1 => "1 steering message is held until the active turn ends.",
            _ => $"{_runs.SteersHeld} steering messages are held until the active turn ends.",
        };
        var pending = _runs.Pending switch
        {
            PendingRunInput.FollowUp => _runs.QueuedFollowUpCount == 1
                ? "1 follow-up is queued. It starts when the active run completes."
                : $"{_runs.QueuedFollowUpCount} follow-ups are queued. They start one at a time when the active run completes.",
            PendingRunInput.Steering => "A steering message is being delivered to the active run.",
            _ => _runs.IsRunActive
                ? "A run is active. Send waits until the active turn ends; Follow-up waits until the run completes."
                : string.Empty,
        };

        if (held.Length == 0)
        {
            return pending;
        }

        return pending.Length == 0 ? held : $"{held} {pending}";
    }
}
