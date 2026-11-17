using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// A permission request parked on a turn is cancelled when that turn is cancelled, abandoned or disposed. The resolver's
/// token is cancelled, and the answer is the cancelled outcome: an allow the resolver releases afterwards never reaches the
/// agent as selected. A decision made before any cancel still answers with the selected option.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
public sealed class AcpParkedPermissionCancelTests
{
    private const long PermissionRequestId = 910;

    private const string AllowAndRejectOptions =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"},{\"optionId\":\"reject-once\",\"name\":\"Reject\",\"kind\":\"reject_once\"}]";

    private static readonly TimeSpan DecisionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    /// <summary>
    /// CancelAsync cancels the wait parked on the turn. An allow released afterwards is still answered as the cancelled outcome,
    /// and the held prompt then completes as cancelled.
    /// </summary>
    [Fact]
    public async Task CancelAsyncCancelsParkedWaitAndAnswersCancelledEvenWhenAllowArrivesLate()
    {
        var ct = TestContext.Current.CancellationToken;
        var parked = await StartParkedTurnAsync(ct).ConfigureAwait(true);
        await using var sessionScope = parked.Session.ConfigureAwait(true);

        await parked.Session.CancelAsync(ct).ConfigureAwait(true);
        parked.Resolver.Token.IsCancellationRequested.ShouldBeTrue();

        parked.Resolver.Release(PermissionDecision.Allow);
        var answer = await parked.Answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        var outcome = OutcomeOf(answer);
        outcome["outcome"]!.GetValue<string>().ShouldBe("cancelled");
        outcome["optionId"].ShouldBeNull();

        parked.Transport.Deliver(PromptResponse(parked.PromptId, "cancelled"));
        var rest = await DrainAsync(parked.Stream).WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        rest.ShouldBe(new HarnessEvent[] { new CompletedEvent("cancelled") });
    }

    /// <summary>
    /// Abandoning the turn (the consumer stops reading and disposes the stream) cancels the wait parked on it. An allow released
    /// afterwards is still answered as the cancelled outcome, and the cancel is sent to the agent.
    /// </summary>
    [Fact]
    public async Task AbandonTurnCancelsParkedWaitAndAnswersCancelledEvenWhenAllowArrivesLate()
    {
        var ct = TestContext.Current.CancellationToken;
        var parked = await StartParkedTurnAsync(ct).ConfigureAwait(true);
        await using var sessionScope = parked.Session.ConfigureAwait(true);

        await parked.Stream.DisposeAsync().ConfigureAwait(true);
        parked.Resolver.Token.IsCancellationRequested.ShouldBeTrue();
        parked.Transport.Sent.Any(static frame => string.Equals(AcpFixtures.MethodOf(frame), "session/cancel", StringComparison.Ordinal)).ShouldBeTrue();

        parked.Resolver.Release(PermissionDecision.Allow);
        var answer = await parked.Answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        var outcome = OutcomeOf(answer);
        outcome["outcome"]!.GetValue<string>().ShouldBe("cancelled");
        outcome["optionId"].ShouldBeNull();
    }

    /// <summary>
    /// A permission decided with allow before any cancel answers with the selected allow option, and its wait is never cancelled:
    /// the normal path is unchanged.
    /// </summary>
    [Fact]
    public async Task AllowDecidedBeforeAnyCancelAnswersSelectedAndLeavesWaitUncancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var parked = await StartParkedTurnAsync(ct).ConfigureAwait(true);
        await using var sessionScope = parked.Session.ConfigureAwait(true);
        parked.Resolver.Token.IsCancellationRequested.ShouldBeFalse();

        parked.Resolver.Release(PermissionDecision.Allow);
        var answer = await parked.Answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        var outcome = OutcomeOf(answer);
        outcome["outcome"]!.GetValue<string>().ShouldBe("selected");
        outcome["optionId"]!.GetValue<string>().ShouldBe("allow-once");

        parked.Transport.Deliver(PromptResponse(parked.PromptId, "end_turn"));
        var rest = await DrainAsync(parked.Stream).WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        rest.ShouldBe(new HarnessEvent[] { new CompletedEvent("end_turn") });
    }

    /// <summary>
    /// Starts a prompt the agent holds open, admits a permission request for it, and returns once the request has been
    /// announced on the turn and the resolver is parked on it.
    /// </summary>
    private static async Task<ParkedTurn> StartParkedTurnAsync(CancellationToken cancellationToken)
    {
        var promptSent = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (string.Equals(AcpFixtures.MethodOf(frame), "session/prompt", StringComparison.Ordinal))
            {
                promptSent.TrySetResult(AcpFixtures.IdOf(frame));
                return [];
            }

            if (IsPermissionAnswer(frame))
            {
                answered.TrySetResult(frame);
                return [];
            }

            return AcpFixtures.Reply(frame);
        });
        var resolver = new ParkingResolver();
        var options = new AcpClientOptions
        {
            WorkingDirectory = AcpFixtures.CapturedCwd,
            AuthMethodId = "agent",
            ApprovalPolicy = new AcpApprovalPolicy(["execute"]),
        };
        var session = new AcpAgentSession(transport, options, resolver, DecisionTimeout);
        await session.CreateAsync(cancellationToken).ConfigureAwait(true);

        var stream = session.SubmitPromptAsync("work", new PromptOptions(), cancellationToken).GetAsyncEnumerator(cancellationToken);
        var firstMove = stream.MoveNextAsync().AsTask();
        var promptId = await promptSent.Task.WaitAsync(WaitLimit, cancellationToken).ConfigureAwait(true);

        transport.Deliver(PermissionRequest("execute"));

        var more = await firstMove.WaitAsync(WaitLimit, cancellationToken).ConfigureAwait(true);
        while (more && stream.Current is not PermissionRequestEvent)
        {
            more = await stream.MoveNextAsync().ConfigureAwait(true);
        }

        more.ShouldBeTrue();
        (stream.Current is PermissionRequestEvent).ShouldBeTrue();
        await resolver.Entered.WaitAsync(WaitLimit, cancellationToken).ConfigureAwait(true);

        return new ParkedTurn(session, transport, resolver, stream, answered, promptId);
    }

    private static async Task<List<HarnessEvent>> DrainAsync(IAsyncEnumerator<HarnessEvent> stream)
    {
        var rest = new List<HarnessEvent>();
        while (await stream.MoveNextAsync().ConfigureAwait(true))
        {
            rest.Add(stream.Current);
        }

        return rest;
    }

    private static JsonNode OutcomeOf(byte[] answer) => JsonNode.Parse(answer)!["result"]!["outcome"]!;

    private static bool IsPermissionAnswer(byte[] frame) =>
        AcpFixtures.MethodOf(frame) is null && AcpFixtures.IdOf(frame) == PermissionRequestId;

    private static byte[] PermissionRequest(string kind) =>
        AcpFixtures.Utf8(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{PermissionRequestId},\"method\":\"session/request_permission\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"toolCall\":{{\"toolCallId\":\"c1\",\"title\":\"Run it\",\"kind\":\"{kind}\"}},\"options\":{AllowAndRejectOptions}}}}}");

    private static byte[] PromptResponse(long promptId, string stopReason) =>
        AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{promptId},\"result\":{{\"stopReason\":\"{stopReason}\"}}}}");

    /// <summary>The session, transport and held stream of a turn with a parked permission request.</summary>
    private sealed record ParkedTurn(
        AcpAgentSession Session,
        ScriptedAcpTransport Transport,
        ParkingResolver Resolver,
        IAsyncEnumerator<HarnessEvent> Stream,
        TaskCompletionSource<byte[]> Answered,
        long PromptId);

    /// <summary>
    /// A resolver that parks on a task the test releases. It records the cancellation token it was handed and returns the
    /// released decision as given, ignoring that token: a late decision is the one a cancelled wait could still produce.
    /// </summary>
    private sealed class ParkingResolver : IPermissionResolver
    {
        private readonly TaskCompletionSource<PermissionDecision> _decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the cancellation token the resolver was handed for its decision.</summary>
        public CancellationToken Token { get; private set; }

        /// <summary>Gets a task that completes once a decision has been asked for.</summary>
        public Task Entered => _entered.Task.WaitAsync(Timeout.InfiniteTimeSpan);

        /// <summary>Releases the parked decision.</summary>
        /// <param name="decision">The decision to return.</param>
        public void Release(PermissionDecision decision) => _decision.TrySetResult(decision);

        /// <inheritdoc />
        public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            _entered.TrySetResult();
            return _decision.Task.WaitAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);
        }
    }
}
