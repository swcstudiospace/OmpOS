namespace OmpOS.Presentation.Approvals;

/// <summary>The command the agent proposed to the terminal panel (see <see cref="ITerminalCommandProposals"/>).</summary>
public sealed class TerminalProposalEventArgs : EventArgs
{
    /// <summary>Creates the event data for one proposal.</summary>
    /// <param name="proposalId">The panel's identifier for the proposal.</param>
    /// <param name="command">The executable name or path.</param>
    /// <param name="arguments">The arguments, one element each. No shell interprets them.</param>
    /// <param name="workingDirectory">The working directory as proposed, or an empty string for the workspace root.</param>
    /// <param name="startedWithoutApproval">
    /// True when the panel's explicit auto-run opt-in already started the command, so there is nothing to decide.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="command"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> or <paramref name="workingDirectory"/> is null.</exception>
    public TerminalProposalEventArgs(
        Guid proposalId,
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        bool startedWithoutApproval = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(workingDirectory);

        ProposalId = proposalId;
        Command = command;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        StartedWithoutApproval = startedWithoutApproval;
    }

    /// <summary>Gets the panel's identifier for the proposal.</summary>
    public Guid ProposalId { get; }

    /// <summary>Gets the executable name or path.</summary>
    public string Command { get; }

    /// <summary>Gets the arguments, one element each.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>Gets the working directory as proposed, or an empty string for the workspace root.</summary>
    public string WorkingDirectory { get; }

    /// <summary>Gets a value indicating whether the auto-run opt-in already started the command, so no approval is needed.</summary>
    public bool StartedWithoutApproval { get; }
}
