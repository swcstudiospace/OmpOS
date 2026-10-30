using Autonogrammer.Presentation.Approvals;
using Shouldly;
using Xunit;
using Decision = Autonogrammer.Presentation.Approvals.ToolApprovalDecision;
using Outcome = Autonogrammer.Presentation.Approvals.ToolApprovalOutcome;
using Status = Autonogrammer.Presentation.Approvals.ToolApprovalStatus;

namespace Autonogrammer.Presentation.Tests.Approvals;

/// <summary>
/// The terminal panel's command approval (ADE-02, ADE-14): a proposal becomes a <see cref="PendingToolApproval"/> decided
/// through <see cref="ToolApprovalCoordinator"/>, and the fake panel starts a process only after the bridge accepted an allow.
/// </summary>
public sealed class TerminalApprovalAdapterTests
{
    private const string SessionId = "term-1";
    private static readonly Guid ProposalId = new("0f0e0d0c-0b0a-0908-0706-050403020100");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A proposal is a pending request, and only an accepted allow through the bridge starts the command, once.</summary>
    [Fact]
    public async Task ATerminalCommandIsDecidedThroughTheApprovalBridge()
    {
        var log = new List<string>();
        var bridge = new ScriptedBridge(log, Outcome.Accepted);
        var panel = new FakePanel(log);
        using var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(bridge), SessionId);
        var raised = new List<PendingToolApproval>();
        adapter.ApprovalRaised += (_, args) => raised.Add(args.Approval);

        panel.Propose(ProposalId, "dotnet", ["build", "-c", "Release"], "src");
        var approval = adapter.Approvals.ShouldHaveSingleItem();
        approval.Status.ShouldBe(Status.Pending);
        approval.SessionId.ShouldBe(SessionId);
        approval.RequestId.ShouldBe("0f0e0d0c-0b0a-0908-0706-050403020100");
        approval.ToolName.ShouldBe(TerminalApprovalAdapter.ToolName);
        approval.ArgumentsSummary.ShouldBe("dotnet build -c Release (in src)");
        raised.ShouldBe([approval]);
        panel.Started.ShouldBeEmpty();

        await adapter.DecideAsync(approval, Decision.Allow, Token);

        approval.Status.ShouldBe(Status.Allowed);
        bridge.Calls.ShouldBe([new BridgeCall(SessionId, "0f0e0d0c-0b0a-0908-0706-050403020100", Decision.Allow)]);
        panel.Started.ShouldBe([ProposalId]);
        panel.Declined.ShouldBeEmpty();
        log.ShouldBe(["bridge:Allow", "start"]);
    }

    /// <summary>A proposal that nobody decided starts no process.</summary>
    [Fact]
    public void AProposalWithNoDecisionStartsNoProcess()
    {
        var panel = new FakePanel([]);
        using var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(new ScriptedBridge([], Outcome.Accepted)), SessionId);

        panel.Propose(ProposalId, "rm", ["-rf", "build"], string.Empty);

        adapter.Approvals.ShouldHaveSingleItem().Status.ShouldBe(Status.Pending);
        panel.Started.ShouldBeEmpty();
        panel.Declined.ShouldBeEmpty();
    }

    /// <summary>Every bridge answer other than an accepted allow starts nothing, and a refusal also declines nothing.</summary>
    /// <param name="outcome">The bridge outcome for the allow.</param>
    /// <param name="expected">The status the request must reach.</param>
    [Theory]
    [InlineData(Outcome.Refused, Status.Refused)]
    [InlineData(Outcome.Failed, Status.Failed)]
    [InlineData(Outcome.NotFound, Status.NotFound)]
    [InlineData(Outcome.AlreadyDecided, Status.AlreadyDecided)]
    public async Task NoBridgeDecisionStartsNoProcess(Outcome outcome, Status expected)
    {
        var panel = new FakePanel([]);
        using var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(new ScriptedBridge([], outcome)), SessionId);
        panel.Propose(ProposalId, "dotnet", ["test"], string.Empty);
        var approval = adapter.Approvals.ShouldHaveSingleItem();

        await adapter.DecideAsync(approval, Decision.Allow, Token);

        approval.Status.ShouldBe(expected);
        panel.Started.ShouldBeEmpty();
        panel.Declined.ShouldBeEmpty();
    }

    /// <summary>A bridge that fails by throwing leaves the proposal pending and starts nothing.</summary>
    [Fact]
    public async Task ABridgeThatThrowsStartsNoProcess()
    {
        var panel = new FakePanel([]);
        using var adapter = new TerminalApprovalAdapter(
            panel,
            new ToolApprovalCoordinator(new ThrowingBridge()),
            SessionId);
        panel.Propose(ProposalId, "dotnet", ["test"], string.Empty);
        var approval = adapter.Approvals.ShouldHaveSingleItem();

        await Should.ThrowAsync<InvalidOperationException>(() => adapter.DecideAsync(approval, Decision.Allow, Token));

        approval.Status.ShouldBe(Status.Pending);
        panel.Started.ShouldBeEmpty();
    }

    /// <summary>An accepted deny declines the proposal and never starts it.</summary>
    [Fact]
    public async Task AnAcceptedDenyDeclinesTheCommandAndStartsNothing()
    {
        var panel = new FakePanel([]);
        using var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(new ScriptedBridge([], Outcome.Accepted)), SessionId);
        panel.Propose(ProposalId, "dotnet", ["publish"], string.Empty);
        var approval = adapter.Approvals.ShouldHaveSingleItem();

        await adapter.DecideAsync(approval, Decision.Deny, Token);

        approval.Status.ShouldBe(Status.Denied);
        panel.Declined.ShouldBe([ProposalId]);
        panel.Started.ShouldBeEmpty();
    }

    /// <summary>A refused deny declines nothing, so the command stays pending in the panel.</summary>
    [Fact]
    public async Task ARefusedDenyDoesNotDeclineTheCommand()
    {
        var panel = new FakePanel([]);
        using var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(new ScriptedBridge([], Outcome.Refused)), SessionId);
        panel.Propose(ProposalId, "dotnet", ["publish"], string.Empty);
        var approval = adapter.Approvals.ShouldHaveSingleItem();

        await adapter.DecideAsync(approval, Decision.Deny, Token);

        approval.Status.ShouldBe(Status.Refused);
        panel.Declined.ShouldBeEmpty();
        panel.Started.ShouldBeEmpty();
    }

    /// <summary>A refused allow may be retried; the accepted retry starts the command exactly once, and a further decision does not.</summary>
    [Fact]
    public async Task ARetriedAllowStartsTheCommandOnceAndASecondDecisionStartsNothingMore()
    {
        var bridge = new ScriptedBridge([], Outcome.Refused, Outcome.Accepted, Outcome.Accepted);
        var panel = new FakePanel([]);
        using var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(bridge), SessionId);
        panel.Propose(ProposalId, "dotnet", ["build"], string.Empty);
        var approval = adapter.Approvals.ShouldHaveSingleItem();

        await adapter.DecideAsync(approval, Decision.Allow, Token);
        panel.Started.ShouldBeEmpty();
        await adapter.DecideAsync(approval, Decision.Allow, Token);
        await adapter.DecideAsync(approval, Decision.Allow, Token);
        await adapter.DecideAsync(approval, Decision.Deny, Token);

        approval.Status.ShouldBe(Status.Allowed);
        panel.Started.ShouldBe([ProposalId]);
        panel.Declined.ShouldBeEmpty();
        bridge.Calls.Count.ShouldBe(2);
    }

    /// <summary>A command the panel's auto-run already started is not an approval request.</summary>
    [Fact]
    public void ACommandStartedByAutoRunRaisesNoApprovalRequest()
    {
        var panel = new FakePanel([]);
        using var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(new ScriptedBridge([], Outcome.Accepted)), SessionId);

        panel.Propose(ProposalId, "dotnet", ["build"], string.Empty, startedWithoutApproval: true);

        adapter.Approvals.ShouldBeEmpty();
    }

    /// <summary>The same proposal raised twice is one request.</summary>
    [Fact]
    public void ASecondEventForTheSameProposalDoesNotRaiseASecondRequest()
    {
        var panel = new FakePanel([]);
        using var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(new ScriptedBridge([], Outcome.Accepted)), SessionId);

        panel.Propose(ProposalId, "dotnet", ["build"], string.Empty);
        panel.Propose(ProposalId, "dotnet", ["build"], string.Empty);

        adapter.Approvals.Count.ShouldBe(1);
    }

    /// <summary>A command with neither arguments nor a directory is summarized by its name alone.</summary>
    [Fact]
    public void ABareCommandIsSummarizedByItsName()
    {
        var panel = new FakePanel([]);
        using var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(new ScriptedBridge([], Outcome.Accepted)), SessionId);

        panel.Propose(ProposalId, "whoami", [], string.Empty);

        adapter.Approvals.ShouldHaveSingleItem().ArgumentsSummary.ShouldBe("whoami");
    }

    /// <summary>A request that another adapter raised cannot be decided here.</summary>
    [Fact]
    public async Task ARequestFromAnotherSourceIsRejectedAndNothingIsSent()
    {
        var bridge = new ScriptedBridge([], Outcome.Accepted);
        var panel = new FakePanel([]);
        using var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(bridge), SessionId);
        var foreign = new PendingToolApproval(SessionId, "perm-9", "shell", "ls");

        await Should.ThrowAsync<ArgumentException>(() => adapter.DecideAsync(foreign, Decision.Allow, Token));

        bridge.Calls.ShouldBeEmpty();
        foreign.Status.ShouldBe(Status.Pending);
    }

    /// <summary>A disposed adapter no longer listens for proposals.</summary>
    [Fact]
    public void ADisposedAdapterRaisesNoMoreRequests()
    {
        var panel = new FakePanel([]);
        var adapter = new TerminalApprovalAdapter(panel, new ToolApprovalCoordinator(new ScriptedBridge([], Outcome.Accepted)), SessionId);

        adapter.Dispose();
        adapter.Dispose();
        panel.Propose(ProposalId, "dotnet", ["build"], string.Empty);

        adapter.Approvals.ShouldBeEmpty();
    }

    /// <summary>The adapter rejects missing collaborators and a blank session.</summary>
    [Fact]
    public void InvalidArgumentsAreRejected()
    {
        var coordinator = new ToolApprovalCoordinator(new ScriptedBridge([], Outcome.Accepted));
        var panel = new FakePanel([]);

        Should.Throw<ArgumentNullException>(() => new TerminalApprovalAdapter(null!, coordinator, SessionId));
        Should.Throw<ArgumentNullException>(() => new TerminalApprovalAdapter(panel, null!, SessionId));
        Should.Throw<ArgumentException>(() => new TerminalApprovalAdapter(panel, coordinator, " "));
    }

    private sealed record BridgeCall(string SessionId, string RequestId, Decision Decision);

    /// <summary>Answers each call with the next scripted outcome (the last one repeats) and records what it was asked.</summary>
    private sealed class ScriptedBridge(List<string> log, params Outcome[] outcomes) : IToolApprovalBridge
    {
        private int _next;

        public List<BridgeCall> Calls { get; } = [];

        public Task<Outcome> DecideAsync(string sessionId, string requestId, Decision decision, CancellationToken cancellationToken)
        {
            Calls.Add(new BridgeCall(sessionId, requestId, decision));
            log.Add($"bridge:{decision}");
            var outcome = outcomes[Math.Min(_next, outcomes.Length - 1)];
            _next++;
            return Task.FromResult(outcome);
        }
    }

    private sealed class ThrowingBridge : IToolApprovalBridge
    {
        public Task<Outcome> DecideAsync(string sessionId, string requestId, Decision decision, CancellationToken cancellationToken) =>
            Task.FromException<Outcome>(new InvalidOperationException("The bridge is down."));
    }

    /// <summary>A terminal panel that starts no process: it only records which proposals it was told to start or decline.</summary>
    private sealed class FakePanel(List<string> log) : ITerminalCommandProposals
    {
        public event EventHandler<TerminalProposalEventArgs>? ProposalRaised;

        public List<Guid> Started { get; } = [];

        public List<Guid> Declined { get; } = [];

        public void Propose(Guid id, string command, IReadOnlyList<string> arguments, string workingDirectory, bool startedWithoutApproval = false) =>
            ProposalRaised?.Invoke(this, new TerminalProposalEventArgs(id, command, arguments, workingDirectory, startedWithoutApproval));

        public Task StartAsync(Guid proposalId, CancellationToken cancellationToken)
        {
            log.Add("start");
            Started.Add(proposalId);
            return Task.CompletedTask;
        }

        public void Decline(Guid proposalId)
        {
            log.Add("decline");
            Declined.Add(proposalId);
        }
    }
}
