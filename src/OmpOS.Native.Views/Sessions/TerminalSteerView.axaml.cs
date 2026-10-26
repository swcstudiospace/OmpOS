using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Runs;

namespace OmpOS.Native.Views.Sessions;

/// <summary>
/// The composer of an omp terminal session. The terminal takes the keys; this box only steers the running omp. <c>Send now</c> (Enter or
/// Ctrl+Enter) delivers the message into the running turn through the session's steering input, <c>Queue</c> (Ctrl+Shift+Enter) runs it
/// after the turn, and Shift+Enter adds a line. A message that was not delivered stays in the box, and the reason goes to the status line
/// of the session through <see cref="StatusMessage"/>. The view reads the <see cref="SteeringInputModel"/> of the
/// <see cref="AgentSessionViewModel"/> that is its data context.
/// </summary>
internal partial class TerminalSteerView : UserControl
{
    private int _inFlight;

    /// <summary>Creates the steer-only composer with no data context.</summary>
    public TerminalSteerView()
    {
        InitializeComponent();
        MessageInput.AddHandler(KeyDownEvent, OnMessageKeyDown, RoutingStrategies.Tunnel);
        SendNowButton.Click += OnSendNowClick;
        QueueButton.Click += OnQueueClick;
    }

    /// <summary>Raised on the UI thread with a sentence for the session status line, for example that omp is not running.</summary>
    public event EventHandler<ComposerStatusEventArgs>? StatusMessage;

    /// <summary>Raised on the UI thread once a send has settled: the delivery ended and the box holds what it should.</summary>
    public event EventHandler? WorkSettled;

    /// <summary>Gets whether a send is waiting for omp's acknowledgement.</summary>
    public bool IsWorking => _inFlight > 0;

    /// <summary>Gets the text of the message box.</summary>
    public string Draft => MessageInput.Text ?? string.Empty;

    /// <summary>Moves keyboard focus to the message box.</summary>
    public void FocusMessage() => MessageInput.FocusWhenReady();

    private void OnMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var shift = (e.KeyModifiers & KeyModifiers.Shift) != KeyModifiers.None;
        var ctrl = (e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.None;
        if (shift && !ctrl)
        {
            // Shift+Enter is left alone, so the text box inserts the newline.
            return;
        }

        e.Handled = true;
        _ = SubmitAsync(ctrl && shift ? SteeringMode.FollowUp : SteeringMode.Steer);
    }

    private void OnSendNowClick(object? sender, RoutedEventArgs e) => _ = SubmitAsync(SteeringMode.Steer);

    private void OnQueueClick(object? sender, RoutedEventArgs e) => _ = SubmitAsync(SteeringMode.FollowUp);

    private async Task SubmitAsync(SteeringMode mode)
    {
        if ((DataContext as AgentSessionViewModel)?.Steering is not { } steering || _inFlight > 0)
        {
            return;
        }

        var text = Draft;
        _inFlight++;
        try
        {
            var outcome = await steering.SubmitAsync(text, mode, CancellationToken.None).ConfigureAwait(true);
            switch (outcome)
            {
                case SteeringSubmitOutcome.Accepted:
                    // Clear only the text that was sent: text typed while the delivery waited is not the sent text.
                    if (string.Equals(Draft, text, StringComparison.Ordinal))
                    {
                        MessageInput.Text = string.Empty;
                    }

                    break;
                case SteeringSubmitOutcome.NoActiveRun:
                    Say(AgentPanelCopy.TerminalNotRunning);
                    break;
                case SteeringSubmitOutcome.QueueFull:
                    Say(steering.RefusalText ?? AgentPanelCopy.QueueFull(RunInputScheduler.MaxQueuedMessages));
                    break;
                default:
                    // Failed: the session already put the reason on the status line, and the text stays where the user typed it.
                    break;
            }
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            Say($"{MainView.FaultedStatus} {fault.GetBaseException().Message}");
        }
        finally
        {
            _inFlight--;
            WorkSettled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Say(string message) => StatusMessage?.Invoke(this, new ComposerStatusEventArgs(message));
}
