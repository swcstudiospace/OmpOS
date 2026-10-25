using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmpOS.Harness.Terminal;

namespace OmpOS.Native.Views.Terminal;

/// <summary>
/// The terminal panel (ADE-14, ADR-0006). A command is proposed with its arguments and working directory, and waits for
/// Approve or Decline. Only Approve runs it, through <see cref="TerminalGate"/>. The output control shows what approved
/// runs print. It is not attached to any process, so a key typed in it reaches no shell.
/// </summary>
internal partial class TerminalPanelView : UserControl
{
    private readonly TerminalGate _gate;
    private readonly PtyProcessRunner? _pty;
    private XTerm.Terminal? _hookedTerminal;
    private TerminalCommand? _pending;
    private CancellationTokenSource? _running;
    private string _status = string.Empty;

    /// <summary>
    /// Creates a panel for one workspace.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root. It must exist. Commands run in it unless a proposal names a directory inside it.</param>
    /// <param name="settings">The terminal settings. Null means the defaults, which never run a command without approval.</param>
    /// <param name="runner">
    /// Runs approved commands. Null means a pseudo-terminal runner that streams output to this panel's output control.
    /// </param>
    /// <exception cref="System.IO.DirectoryNotFoundException">The workspace root does not exist.</exception>
    public TerminalPanelView(string workspaceRoot, TerminalSettings? settings = null, IProcessRunner? runner = null)
    {
        InitializeComponent();

        var processRunner = runner ?? new PtyProcessRunner(new OutputSink(this));
        _pty = processRunner as PtyProcessRunner;
        _gate = new TerminalGate(workspaceRoot, settings ?? new TerminalSettings(), processRunner);

        ProposeButton.Click += OnProposeClick;
        ApproveButton.Click += OnApproveClick;
        DeclineButton.Click += OnDeclineClick;
        StopButton.Click += OnStopClick;
        // The control starts its default shell when it loads unless Process is empty. This panel must never start a shell
        // itself: every process runs through the gate, so Process stays empty (ADR-0006).
        Output.Process = string.Empty;
        Refresh();
    }

    private void OnProposeClick(object? sender, RoutedEventArgs e)
    {
        if (_pending is not null || _running is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CommandInput.Text))
        {
            _status = "Enter a command to propose.";
            Refresh();
            return;
        }

        Observe(ProposeAsync());
    }

    private async Task ProposeAsync()
    {
        try
        {
            var command = await _gate.ProposeAsync(BuildProposal(), CancellationToken.None).ConfigureAwait(true);
            if (command.Status == CommandStatus.Pending)
            {
                _pending = command;
                _status = "Waiting for approval. Nothing has run.";
            }
            else
            {
                ShowResult(command);
            }
        }
        catch (CommandProposalRejectedException exception)
        {
            _status = exception.Message;
        }

        Refresh();
    }

    private void OnApproveClick(object? sender, RoutedEventArgs e)
    {
        if (_pending is null || _running is not null)
        {
            return;
        }

        Observe(ApproveAsync(_pending));
    }

    private async Task ApproveAsync(TerminalCommand pending)
    {
        var cancellation = new CancellationTokenSource();
        _running = cancellation;
        _pending = null;
        _status = "Running the approved command.";
        try
        {
            EmulatorForRun();
            AppendOutput($"$ {Describe(pending)}\r\n");
            Refresh();

            var finished = await _gate.ApproveAsync(pending.Id, cancellation.Token).ConfigureAwait(true);
            ShowResult(finished);
        }
        catch (OperationCanceledException)
        {
            _status = "Stopped. The process was killed.";
        }
        finally
        {
            _running = null;
            cancellation.Dispose();
            Refresh();
        }
    }

    private void OnDeclineClick(object? sender, RoutedEventArgs e)
    {
        if (_pending is null || _running is not null)
        {
            return;
        }

        var declined = _gate.Decline(_pending.Id);
        _pending = null;
        ShowResult(declined);
        Refresh();
    }

    private void OnStopClick(object? sender, RoutedEventArgs e) => _running?.Cancel();

    /// <summary>Marshals a task's fault to the UI thread, where it is shown rather than lost.</summary>
    private void Observe(Task work)
    {
        _ = work.ContinueWith(
            faulted => Dispatcher.UIThread.Post(() => ReportFault(faulted.Exception)),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private void ReportFault(AggregateException? exception)
    {
        _status = $"Could not finish: {exception?.GetBaseException().Message ?? "unknown error"}";
        Refresh();
    }

    private CommandProposal BuildProposal()
    {
        var arguments = (ArgumentsInput.Text ?? string.Empty)
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0)
            .ToArray();

        return new CommandProposal(
            (CommandInput.Text ?? string.Empty).Trim(),
            arguments,
            (DirectoryInput.Text ?? string.Empty).Trim());
    }

    private void ShowResult(TerminalCommand command)
    {
        if (command.ExitCode is int exitCode)
        {
            AppendOutput($"[exit {exitCode}]\r\n");
        }

        _status = command.Status switch
        {
            CommandStatus.Completed => $"Completed with exit code {command.ExitCode}.",
            CommandStatus.Failed when command.ExitCode is not null => $"Failed with exit code {command.ExitCode}.",
            CommandStatus.Failed => $"Not run: {command.StandardError}",
            CommandStatus.Declined => "Declined. Nothing ran.",
            _ => "Waiting for approval. Nothing has run.",
        };
    }

    private void Refresh()
    {
        var idle = _running is null;
        var waiting = _pending is not null;

        CommandInput.IsEnabled = idle && !waiting;
        ArgumentsInput.IsEnabled = idle && !waiting;
        DirectoryInput.IsEnabled = idle && !waiting;
        ProposeButton.IsEnabled = idle && !waiting;
        ApproveButton.IsEnabled = idle && waiting;
        DeclineButton.IsEnabled = idle && waiting;
        StopButton.IsEnabled = !idle;

        PendingText.Text = _pending is null
            ? "No command is waiting for approval."
            : $"Waiting for approval: {Describe(_pending)}, in {_pending.WorkingDirectory}";
        StatusText.Text = _status;
    }

    private void AppendOutput(string text) => _hookedTerminal?.Write(text);

    /// <summary>
    /// Returns the emulator that draws the output, and makes the pseudo-terminal match its grid. The control creates its emulator
    /// when its template is applied, so this is read when a run starts, never in the constructor. Resizing the grid resizes
    /// the pseudo-terminal of a run in progress.
    /// </summary>
    private XTerm.Terminal EmulatorForRun()
    {
        if (_hookedTerminal is null)
        {
            var terminal = Output.Terminal ?? throw new InvalidOperationException("The terminal control has no emulator to draw into.");
            terminal.Resized += (_, _) => SizePseudoTerminal(terminal);
            _hookedTerminal = terminal;
        }

        SizePseudoTerminal(_hookedTerminal);
        return _hookedTerminal;
    }

    private void SizePseudoTerminal(XTerm.Terminal terminal)
    {
        if (_pty is not null && terminal is { Cols: >= 1, Rows: >= 1 })
        {
            _pty.Resize(terminal.Cols, terminal.Rows);
        }
    }

    private static string Describe(TerminalCommand command) =>
        string.Join(' ', new[] { command.Command }.Concat(command.Arguments.Select(Quote)));

    private static string Quote(string argument) =>
        argument.Length == 0 || argument.Any(char.IsWhiteSpace) ? $"'{argument}'" : argument;

    /// <summary>Receives streamed output from a background thread and hands it to the output control on the UI thread.</summary>
    private sealed class OutputSink(TerminalPanelView view) : IProcessOutputSink
    {
        /// <inheritdoc />
        public void Write(string text) => Dispatcher.UIThread.Post(() => view.AppendOutput(text));
    }
}
