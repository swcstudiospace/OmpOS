using System.Globalization;

namespace OmpOS.Presentation.Approvals;

/// <summary>
/// Turns each command the terminal panel proposes into a <see cref="PendingToolApproval"/> and decides it through
/// <see cref="ToolApprovalCoordinator"/> (ADE-02, ADE-14). A command is started only after the bridge accepted an allow, and
/// it is declined only after the bridge accepted a deny. A refused, failed, not-found or already-decided answer starts and
/// declines nothing, so the proposal stays pending in the panel.
/// </summary>
/// <remarks>
/// A proposal that the panel's auto-run opt-in already started is not an approval: it raises no request. The adapter is not
/// thread-safe; it runs on the UI thread that owns the presentation models, like the coordinator.
/// </remarks>
public sealed class TerminalApprovalAdapter : IDisposable
{
    /// <summary>The tool name shown on every terminal approval request.</summary>
    public const string ToolName = "terminal.run";

    private readonly ITerminalCommandProposals _proposals;
    private readonly ToolApprovalCoordinator _coordinator;
    private readonly string _sessionId;
    private readonly List<PendingToolApproval> _approvals = [];
    private readonly Dictionary<PendingToolApproval, Entry> _entries = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Guid> _seen = [];
    private bool _disposed;

    /// <summary>Creates an adapter and starts listening for proposals.</summary>
    /// <param name="proposals">The terminal panel's proposals.</param>
    /// <param name="coordinator">Decides each request through the permission bridge.</param>
    /// <param name="sessionId">The session every terminal approval request belongs to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="proposals"/> or <paramref name="coordinator"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null, empty or white space.</exception>
    public TerminalApprovalAdapter(ITerminalCommandProposals proposals, ToolApprovalCoordinator coordinator, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        _proposals = proposals;
        _coordinator = coordinator;
        _sessionId = sessionId;
        _proposals.ProposalRaised += OnProposalRaised;
    }

    /// <summary>Occurs when a proposed command became a pending approval request.</summary>
    public event EventHandler<ApprovalRaisedEventArgs>? ApprovalRaised;

    /// <summary>Gets every approval request raised so far, in proposal order.</summary>
    public IReadOnlyList<PendingToolApproval> Approvals => _approvals;

    /// <summary>
    /// Decides one request through the coordinator. An accepted allow starts the command once; an accepted deny declines it.
    /// Any other outcome starts and declines nothing.
    /// </summary>
    /// <param name="approval">A request this adapter raised.</param>
    /// <param name="decision">The user's decision.</param>
    /// <param name="cancellationToken">Cancels the decision call, and the command it starts.</param>
    /// <returns>A task that completes when the decision is recorded and, for an allow, when the command has finished.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="approval"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="approval"/> was not raised by this adapter.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decision"/> is not a defined value.</exception>
    public async Task DecideAsync(PendingToolApproval approval, ToolApprovalDecision decision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (!_entries.TryGetValue(approval, out var entry))
        {
            throw new ArgumentException("The request was not raised by this terminal adapter.", nameof(approval));
        }

        if (entry.Settled)
        {
            return;
        }

        await _coordinator.DecideAsync(approval, decision, cancellationToken).ConfigureAwait(true);

        switch (approval.Status)
        {
            case ToolApprovalStatus.Allowed when !entry.Settled:
                entry.Settled = true;
                await _proposals.StartAsync(entry.ProposalId, cancellationToken).ConfigureAwait(true);
                break;
            case ToolApprovalStatus.Denied when !entry.Settled:
                entry.Settled = true;
                _proposals.Decline(entry.ProposalId);
                break;
            default:
                break;
        }
    }

    /// <summary>Stops listening for proposals.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _proposals.ProposalRaised -= OnProposalRaised;
    }

    private void OnProposalRaised(object? sender, TerminalProposalEventArgs e)
    {
        if (e.StartedWithoutApproval || !_seen.Add(e.ProposalId))
        {
            return;
        }

        var approval = new PendingToolApproval(
            _sessionId,
            e.ProposalId.ToString("D", CultureInfo.InvariantCulture),
            ToolName,
            Summarize(e));
        _approvals.Add(approval);
        _entries.Add(approval, new Entry(e.ProposalId));
        ApprovalRaised?.Invoke(this, new ApprovalRaisedEventArgs(approval));
    }

    private static string Summarize(TerminalProposalEventArgs e)
    {
        var commandLine = e.Arguments.Count == 0
            ? e.Command
            : string.Join(' ', e.Arguments.Prepend(e.Command));
        return e.WorkingDirectory.Length == 0
            ? commandLine
            : $"{commandLine} (in {e.WorkingDirectory})";
    }

    private sealed class Entry(Guid proposalId)
    {
        public Guid ProposalId { get; } = proposalId;

        public bool Settled { get; set; }
    }
}
