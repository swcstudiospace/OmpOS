namespace OmpOS.Presentation.Approvals;

/// <summary>
/// The terminal panel's side of a command approval (ADE-02, ADE-14): the panel's gate raises each command the agent proposes,
/// and starts or declines it only when told to. Presentation cannot reference the terminal harness, so the head adapts its
/// terminal runner to this interface and <see cref="TerminalApprovalAdapter"/> decides each proposal through
/// <see cref="ToolApprovalCoordinator"/>.
/// </summary>
/// <remarks>
/// An implementation must start no process for a proposal until <see cref="StartAsync"/> is called for it, and must raise
/// <see cref="ProposalRaised"/> and complete both methods on the UI thread that owns the presentation models.
/// </remarks>
public interface ITerminalCommandProposals
{
    /// <summary>Occurs when the agent proposes a command. The command is pending: nothing has been started for it.</summary>
    event EventHandler<TerminalProposalEventArgs>? ProposalRaised;

    /// <summary>Starts an approved proposal once and completes when the command has finished.</summary>
    /// <param name="proposalId">The proposal to start.</param>
    /// <param name="cancellationToken">Cancels the run; the process is killed.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    /// <exception cref="KeyNotFoundException">No proposal has this identifier.</exception>
    /// <exception cref="InvalidOperationException">The proposal is not pending, or it is already running.</exception>
    Task StartAsync(Guid proposalId, CancellationToken cancellationToken);

    /// <summary>Declines a pending proposal. No process is started for it.</summary>
    /// <param name="proposalId">The proposal to decline.</param>
    /// <exception cref="KeyNotFoundException">No proposal has this identifier.</exception>
    /// <exception cref="InvalidOperationException">The proposal is not pending, or it is already running.</exception>
    void Decline(Guid proposalId);
}
