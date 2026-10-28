using Autonogrammer.Presentation.Approvals;
using Shouldly;
using Xunit;
using Decision = Autonogrammer.Presentation.Approvals.ToolApprovalDecision;
using Outcome = Autonogrammer.Presentation.Approvals.ToolApprovalOutcome;
using Status = Autonogrammer.Presentation.Approvals.ToolApprovalStatus;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// The ADE-02 decision path on <see cref="ToolApprovalCoordinator"/>, with a scripted <see cref="IToolApprovalBridge"/> and no
/// UI framework. Covers the outcome-to-status mapping, the rule that only an accepted allow reaches
/// <see cref="Status.Allowed"/>, and the single in-flight decision per request.
/// </summary>
public sealed class ToolApprovalCoordinatorTests
{
    private const string SessionId = "s-1";
    private const string RequestId = "perm-1";
    private const string PolicyRefusal = "Policy does not grant this tool.";
    private static readonly CancellationToken NoCancellation = CancellationToken.None;

    /// <summary>An accepted allow is the path to Allowed, and it sends the request identifiers with the decision.</summary>
    [Fact]
    public async Task AcceptedAllowRecordsAllowedAndSendsTheRequestIdentifiers()
    {
        var bridge = new ScriptedBridge(Outcome.Accepted);
        var coordinator = new ToolApprovalCoordinator(bridge);
        var approval = NewRequest();

        await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);

        bridge.Calls.ShouldBe([new BridgeCall(SessionId, RequestId, Decision.Allow)]);
        approval.Status.ShouldBe(Status.Allowed);
    }

    /// <summary>Each outcome of an allow maps to exactly one status: only an accepted allow is Allowed.</summary>
    /// <param name="outcome">The server outcome.</param>
    /// <param name="expected">The status the request must reach.</param>
    [Theory]
    [InlineData(Outcome.Accepted, Status.Allowed)]
    [InlineData(Outcome.Refused, Status.Refused)]
    [InlineData(Outcome.AlreadyDecided, Status.AlreadyDecided)]
    [InlineData(Outcome.NotFound, Status.NotFound)]
    [InlineData(Outcome.Failed, Status.Failed)]
    public async Task AllowDecisionMapsEachOutcomeToItsStatus(Outcome outcome, Status expected)
    {
        var coordinator = new ToolApprovalCoordinator(new ScriptedBridge(outcome));
        var approval = NewRequest();

        await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);

        approval.Status.ShouldBe(expected);
    }

    /// <summary>Each outcome of a deny maps to its status. Deny is never auto-granted, so no outcome of a deny reaches Allowed.</summary>
    /// <param name="outcome">The server outcome.</param>
    /// <param name="expected">The status the request must reach.</param>
    [Theory]
    [InlineData(Outcome.Accepted, Status.Denied)]
    [InlineData(Outcome.Refused, Status.Refused)]
    [InlineData(Outcome.AlreadyDecided, Status.AlreadyDecided)]
    [InlineData(Outcome.NotFound, Status.NotFound)]
    [InlineData(Outcome.Failed, Status.Failed)]
    public async Task DenyDecisionMapsEachOutcomeAndNeverAllows(Outcome outcome, Status expected)
    {
        var coordinator = new ToolApprovalCoordinator(new ScriptedBridge(outcome));
        var approval = NewRequest();

        await coordinator.DecideAsync(approval, Decision.Deny, NoCancellation);

        approval.Status.ShouldBe(expected);
        approval.Status.ShouldNotBe(Status.Allowed);
    }

    /// <summary>Every outcome other than an accepted allow leaves the request unallowed, including any outcome added later.</summary>
    [Fact]
    public async Task NoOutcomeOtherThanAcceptedReachesAllowed()
    {
        foreach (var outcome in Enum.GetValues<Outcome>().Where(candidate => candidate != Outcome.Accepted))
        {
            var coordinator = new ToolApprovalCoordinator(new ScriptedBridge(outcome));
            var approval = NewRequest();

            await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);

            approval.Status.ShouldNotBe(Status.Allowed, $"outcome {outcome} must not allow the tool");
        }
    }

    /// <summary>A new request is pending, and nothing allows it before the user decides.</summary>
    [Fact]
    public void NewRequestIsPendingWithNoMessage()
    {
        var approval = NewRequest();

        approval.Status.ShouldBe(Status.Pending);
        approval.StatusMessage.ShouldBeEmpty();
    }

    /// <summary>A policy refusal keeps the request pending on the server, so the user may still deny it.</summary>
    [Fact]
    public async Task RefusedAllowLeavesTheRequestOpenForADeny()
    {
        var bridge = new ScriptedBridge(Outcome.Refused, Outcome.Accepted);
        var coordinator = new ToolApprovalCoordinator(bridge);
        var approval = NewRequest();

        await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);
        approval.Status.ShouldBe(Status.Refused);
        approval.StatusMessage.ShouldBe(PolicyRefusal);

        await coordinator.DecideAsync(approval, Decision.Deny, NoCancellation);

        approval.Status.ShouldBe(Status.Denied);
        bridge.Calls.Select(call => call.Decision).ShouldBe([Decision.Allow, Decision.Deny]);
    }

    /// <summary>A failed decision keeps the request pending, and a later attempt that the server accepts allows it.</summary>
    [Fact]
    public async Task FailedDecisionCanBeRetried()
    {
        var bridge = new ScriptedBridge(Outcome.Failed, Outcome.Accepted);
        var coordinator = new ToolApprovalCoordinator(bridge);
        var approval = NewRequest();

        await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);
        approval.Status.ShouldBe(Status.Failed);

        await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);

        approval.Status.ShouldBe(Status.Allowed);
        bridge.Calls.Count.ShouldBe(2);
    }

    /// <summary>A request whose decision is final is not sent again, and its status does not change.</summary>
    [Fact]
    public async Task DecidedRequestIsNotSentAgain()
    {
        var bridge = new ScriptedBridge(Outcome.Accepted);
        var coordinator = new ToolApprovalCoordinator(bridge);
        var approval = NewRequest();

        await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);
        await coordinator.DecideAsync(approval, Decision.Deny, NoCancellation);

        bridge.Calls.Count.ShouldBe(1);
        approval.Status.ShouldBe(Status.Allowed);
    }

    /// <summary>A request the server reports as already decided or unknown is final: it is not sent again.</summary>
    /// <param name="outcome">The final server outcome.</param>
    /// <param name="expected">The status the request must keep.</param>
    [Theory]
    [InlineData(Outcome.AlreadyDecided, Status.AlreadyDecided)]
    [InlineData(Outcome.NotFound, Status.NotFound)]
    public async Task FinalOutcomeIsNotSentAgain(Outcome outcome, Status expected)
    {
        var bridge = new ScriptedBridge(outcome, Outcome.Accepted);
        var coordinator = new ToolApprovalCoordinator(bridge);
        var approval = NewRequest();

        await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);
        await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);

        bridge.Calls.Count.ShouldBe(1);
        approval.Status.ShouldBe(expected);
    }

    /// <summary>A decision in flight is not sent a second time, and the second call does not overwrite its outcome.</summary>
    [Fact]
    public async Task DecisionInFlightIsNotSentAgain()
    {
        using var gate = new SemaphoreSlim(0);
        var bridge = new ScriptedBridge(Outcome.Accepted) { Gate = gate };
        var coordinator = new ToolApprovalCoordinator(bridge);
        var approval = NewRequest();

        var first = coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);
        await coordinator.DecideAsync(approval, Decision.Deny, NoCancellation);

        bridge.Calls.Count.ShouldBe(1);
        approval.Status.ShouldBe(Status.Pending);

        gate.Release();
        await first.ConfigureAwait(true);

        approval.Status.ShouldBe(Status.Allowed);
    }

    /// <summary>An invalid decision value is rejected before anything is sent.</summary>
    [Fact]
    public async Task UndefinedDecisionIsRejectedBeforeSending()
    {
        var bridge = new ScriptedBridge();
        var coordinator = new ToolApprovalCoordinator(bridge);
        var approval = NewRequest();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => coordinator.DecideAsync(approval, (Decision)99, NoCancellation));

        bridge.Calls.ShouldBeEmpty();
        approval.Status.ShouldBe(Status.Pending);
    }

    /// <summary>Each outcome gives the user its own explanation, so no two outcomes read the same.</summary>
    [Fact]
    public async Task EachOutcomeGivesItsOwnMessage()
    {
        var messages = new List<string>();
        foreach (var outcome in Enum.GetValues<Outcome>())
        {
            var coordinator = new ToolApprovalCoordinator(new ScriptedBridge(outcome));
            var approval = NewRequest();

            await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);

            approval.StatusMessage.ShouldNotBeEmpty();
            messages.Add(approval.StatusMessage);
        }

        messages.Distinct(StringComparer.Ordinal).Count().ShouldBe(Enum.GetValues<Outcome>().Length);
    }

    /// <summary>A refused allow shows the policy message, which tells the user why the tool did not run.</summary>
    [Fact]
    public async Task RefusedAllowShowsThePolicyMessage()
    {
        var coordinator = new ToolApprovalCoordinator(new ScriptedBridge(Outcome.Refused));
        var approval = NewRequest();

        await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);

        approval.StatusMessage.ShouldBe(PolicyRefusal);
    }

    /// <summary>A recorded outcome announces its status and message, so a run UI can refresh.</summary>
    [Fact]
    public async Task RecordedOutcomeAnnouncesStatusAndMessage()
    {
        var coordinator = new ToolApprovalCoordinator(new ScriptedBridge(Outcome.Accepted));
        var approval = NewRequest();
        var changed = PropertyChangeRecorder.Record(approval);

        await coordinator.DecideAsync(approval, Decision.Allow, NoCancellation);

        changed.ShouldAnnounce(nameof(PendingToolApproval.Status));
        changed.ShouldAnnounce(nameof(PendingToolApproval.StatusMessage));
    }

    /// <summary>A request without a session identifier cannot be created, so it can never be sent.</summary>
    /// <param name="sessionId">The blank session identifier.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BlankSessionIdentifierIsRejected(string sessionId)
    {
        Should.Throw<ArgumentException>(() => new PendingToolApproval(sessionId, RequestId, "shell", "git status"));
    }

    /// <summary>A coordinator without a bridge cannot be created.</summary>
    [Fact]
    public void NullBridgeIsRejected()
    {
        Should.Throw<ArgumentNullException>(() => new ToolApprovalCoordinator(null!));
    }

    private static PendingToolApproval NewRequest() => new(SessionId, RequestId, "shell", "command: git status");

    private sealed record BridgeCall(string SessionId, string RequestId, Decision Decision);

    /// <summary>Answers each call with the next scripted outcome, optionally holding each call on a gate, and records what it was asked.</summary>
    private sealed class ScriptedBridge(params Outcome[] outcomes) : IToolApprovalBridge
    {
        private readonly Queue<Outcome> _outcomes = new(outcomes);

        /// <summary>Gets the gate each call waits on, or null when calls answer at once.</summary>
        public SemaphoreSlim? Gate { get; init; }

        public List<BridgeCall> Calls { get; } = [];

        public async Task<Outcome> DecideAsync(string sessionId, string requestId, Decision decision, CancellationToken cancellationToken)
        {
            Calls.Add(new BridgeCall(sessionId, requestId, decision));
            if (Gate is not null)
            {
                await Gate.WaitAsync(cancellationToken).ConfigureAwait(true);
            }

            return _outcomes.Dequeue();
        }
    }
}
