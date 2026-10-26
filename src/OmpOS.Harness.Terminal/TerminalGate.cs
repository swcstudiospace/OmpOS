using System.ComponentModel;

namespace OmpOS.Harness.Terminal;

/// <summary>
/// Holds terminal command proposals until they are approved or declined. A proposal is Pending until a decision is
/// made; a declined proposal never reaches the runner. Auto-run runs a proposal without a decision, and it is off
/// unless the settings enable it. A working directory outside the workspace root is rejected before anything is recorded.
/// </summary>
/// <remarks>
/// The start directory of the terminal is <see cref="WorkspaceRoot"/>. A proposal without a working directory runs there.
/// </remarks>
public sealed class TerminalGate
{
    private const string NotCompletedMessage = "The command did not complete.";

    private readonly IProcessRunner _runner;
    private readonly TerminalSettings _settings;
    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, TerminalCommand> _commands = [];
    private readonly List<Guid> _proposalOrder = [];
    private readonly HashSet<Guid> _running = [];

    /// <summary>
    /// Initializes a gate for one workspace.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root directory. It must exist.</param>
    /// <param name="settings">The terminal settings. Auto-run is off unless enabled here.</param>
    /// <param name="runner">Runs approved commands. Called at most once per proposal.</param>
    /// <exception cref="DirectoryNotFoundException">The workspace root does not exist.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The output bound is less than 1.</exception>
    public TerminalGate(string workspaceRoot, TerminalSettings settings, IProcessRunner runner)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(runner);
        if (settings.MaxOutputCharacters < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.MaxOutputCharacters,
                "The output bound must be at least 1 character.");
        }

        WorkspaceRoot = WorkspacePath.ResolveRoot(workspaceRoot);
        _settings = settings;
        _runner = runner;
    }

    /// <summary>
    /// The physical workspace root. The terminal starts here, and an empty working directory means this path.
    /// </summary>
    public string WorkspaceRoot { get; }

    /// <summary>
    /// Every proposal the gate has recorded, in proposal order.
    /// </summary>
    public IReadOnlyList<TerminalCommand> Commands
    {
        get
        {
            lock (_sync)
            {
                return _proposalOrder.Select(id => _commands[id]).ToArray();
            }
        }
    }

    /// <summary>
    /// Records a proposal. It stays Pending until <see cref="ApproveAsync"/> or <see cref="Decline"/> is called,
    /// unless auto-run is enabled, in which case it runs before this returns.
    /// </summary>
    /// <param name="proposal">The command and its working directory.</param>
    /// <param name="cancellationToken">Cancels a command that auto-run starts.</param>
    /// <returns>The recorded proposal, with its result when auto-run ran it.</returns>
    /// <exception cref="ArgumentException">The command is blank.</exception>
    /// <exception cref="CommandProposalRejectedException">
    /// The working directory is outside the workspace root or is not an existing directory. Nothing is recorded.
    /// </exception>
    public async Task<TerminalCommand> ProposeAsync(CommandProposal proposal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (string.IsNullOrWhiteSpace(proposal.Command))
        {
            throw new ArgumentException("The command must not be blank.", nameof(proposal));
        }

        var workingDirectory = ResolveWorkingDirectory(proposal.WorkingDirectory);
        var command = new TerminalCommand(
            Guid.NewGuid(),
            proposal.Command,
            proposal.Arguments.ToArray(),
            workingDirectory,
            CommandStatus.Pending,
            ExitCode: null,
            StandardOutput: string.Empty,
            StandardError: string.Empty,
            StandardOutputTruncated: false,
            StandardErrorTruncated: false);

        lock (_sync)
        {
            _commands.Add(command.Id, command);
            _proposalOrder.Add(command.Id);
        }

        return _settings.AutoRunEnabled
            ? await ExecuteAsync(command.Id, cancellationToken).ConfigureAwait(false)
            : command;
    }

    /// <summary>
    /// Approves a pending proposal and runs it once through the runner.
    /// </summary>
    /// <param name="id">The proposal to approve.</param>
    /// <param name="cancellationToken">Cancels the run; the process is killed.</param>
    /// <returns>The proposal with its result: Completed, or Failed when the exit code was not 0 or the run failed.</returns>
    /// <exception cref="KeyNotFoundException">No proposal has this id.</exception>
    /// <exception cref="InvalidOperationException">The proposal is not Pending, or it is already running.</exception>
    public Task<TerminalCommand> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        return ExecuteAsync(id, cancellationToken);
    }

    /// <summary>
    /// Declines a pending proposal. The runner is never called for it.
    /// </summary>
    /// <param name="id">The proposal to decline.</param>
    /// <returns>The proposal with status Declined.</returns>
    /// <exception cref="KeyNotFoundException">No proposal has this id.</exception>
    /// <exception cref="InvalidOperationException">The proposal is not Pending, or it is already running.</exception>
    public TerminalCommand Decline(Guid id)
    {
        lock (_sync)
        {
            var command = Find(id);
            EnsureDecidable(command);

            var declined = command with { Status = CommandStatus.Declined };
            _commands[id] = declined;
            return declined;
        }
    }

    private async Task<TerminalCommand> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        TerminalCommand command;
        lock (_sync)
        {
            command = Find(id);
            EnsureDecidable(command);
            _running.Add(id);
        }

        var request = new ProcessRequest(
            command.Command,
            command.Arguments,
            command.WorkingDirectory,
            _settings.MaxOutputCharacters);

        // Every path records a terminal state, so a claimed command cannot stay in flight or run again.
        var finished = command with { Status = CommandStatus.Failed, StandardError = NotCompletedMessage };
        try
        {
            var outcome = await _runner.RunAsync(request, cancellationToken).ConfigureAwait(false);
            finished = FromOutcome(command, outcome);
        }
        catch (OperationCanceledException)
        {
            // The runner killed the process, or it never started. Either way the proposal must not run again.
            finished = command with
            {
                Status = CommandStatus.Failed,
                StandardError = "The command was cancelled and its process was stopped.",
            };
            throw;
        }
        catch (Win32Exception exception)
        {
            finished = FailedToRun(command, exception);
        }
        catch (IOException exception)
        {
            finished = FailedToRun(command, exception);
        }
        catch (InvalidOperationException exception)
        {
            finished = FailedToRun(command, exception);
        }
        finally
        {
            Finish(finished);
        }

        return finished;
    }

    private static TerminalCommand FailedToRun(TerminalCommand command, Exception exception)
    {
        return command with
        {
            Status = CommandStatus.Failed,
            ExitCode = null,
            StandardError = exception.Message,
        };
    }

    private TerminalCommand FromOutcome(TerminalCommand command, ProcessOutcome outcome)
    {
        var (standardOutput, outputTruncated) = Bound(outcome.StandardOutput, outcome.StandardOutputTruncated);
        var (standardError, errorTruncated) = Bound(outcome.StandardError, outcome.StandardErrorTruncated);

        return command with
        {
            Status = outcome.ExitCode == 0 ? CommandStatus.Completed : CommandStatus.Failed,
            ExitCode = outcome.ExitCode,
            StandardOutput = standardOutput,
            StandardError = standardError,
            StandardOutputTruncated = outputTruncated,
            StandardErrorTruncated = errorTruncated,
        };
    }

    private (string Text, bool Truncated) Bound(string text, bool alreadyTruncated)
    {
        var maximum = _settings.MaxOutputCharacters;
        return text.Length <= maximum ? (text, alreadyTruncated) : (text[..maximum], true);
    }

    private void Finish(TerminalCommand command)
    {
        lock (_sync)
        {
            _commands[command.Id] = command;
            _running.Remove(command.Id);
        }
    }

    private TerminalCommand Find(Guid id)
    {
        return _commands.TryGetValue(id, out var command)
            ? command
            : throw new KeyNotFoundException($"No terminal command has id {id}.");
    }

    private void EnsureDecidable(TerminalCommand command)
    {
        if (command.Status != CommandStatus.Pending || _running.Contains(command.Id))
        {
            throw new InvalidOperationException(
                $"Command {command.Id} has status {command.Status} or is already running; only a pending command that is not running can be approved or declined.");
        }
    }

    private string ResolveWorkingDirectory(string declared)
    {
        if (!WorkspacePath.TryResolveInside(WorkspaceRoot, declared, out var physical))
        {
            throw new CommandProposalRejectedException(
                $"Working directory '{declared}' is outside the workspace root or is not an existing directory.");
        }

        return physical;
    }
}
