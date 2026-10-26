using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>
/// The permission path consults the approval policy first. A request the policy forbids is answered at once; a request it
/// admits is parked on its id until a decision arrives.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default, and await using disposal is intentional.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class AcpParkedPermissionTests
{
    private const long PermissionRequestId = 900;

    private const string AllowAndRejectOptions =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"},{\"optionId\":\"reject-once\",\"name\":\"Reject\",\"kind\":\"reject_once\"}]";

    private static readonly TimeSpan DecisionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    /// <summary>A request the policy forbids is answered deny at once: the resolver is never asked, so nothing waits for a decision.</summary>
    [Fact]
    public async Task PolicyForbiddenPermissionIsAnsweredDenyImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        var resolver = new GateResolver();
        var options = new AcpClientOptions
        {
            WorkingDirectory = AcpFixtures.CapturedCwd,
            AuthMethodId = "agent",
            ApprovalPolicy = new AcpApprovalPolicy(["read"]),
        };
        await using var session = new AcpAgentSession(transport, options, resolver, DecisionTimeout);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest("execute"));

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("reject-once");
        resolver.Calls.ShouldBe(0);
    }

    /// <summary>A request the policy admits is parked on its id: no answer is sent until a decision arrives, and then it carries that decision.</summary>
    [Fact]
    public async Task PolicyAdmittedPermissionIsParkedUntilDecisionArrives()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        var resolver = new GateResolver();
        var options = new AcpClientOptions
        {
            WorkingDirectory = AcpFixtures.CapturedCwd,
            AuthMethodId = "agent",
            ApprovalPolicy = new AcpApprovalPolicy(["execute"]),
        };
        await using var session = new AcpAgentSession(transport, options, resolver, DecisionTimeout);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest("execute"));

        await resolver.WaitForEntryAsync(WaitLimit, ct).ConfigureAwait(true);
        var request = resolver.Request.ShouldNotBeNull();
        request.RequestId.ShouldBe("900");
        answered.Task.IsCompleted.ShouldBeFalse();
        transport.Sent.Any(IsPermissionAnswer).ShouldBeFalse();

        resolver.Answer(PermissionDecision.Allow);

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("allow-once");
    }

    private static ScriptedAcpTransport RecordingTransport(TaskCompletionSource<byte[]> answered) =>
        new(frame =>
        {
            if (IsPermissionAnswer(frame))
            {
                answered.TrySetResult(frame);
            }

            return AcpFixtures.Reply(frame);
        });

    private static bool IsPermissionAnswer(byte[] frame) =>
        AcpFixtures.MethodOf(frame) is null && AcpFixtures.IdOf(frame) == PermissionRequestId;

    private static byte[] PermissionRequest(string kind) =>
        AcpFixtures.Utf8(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{PermissionRequestId},\"method\":\"session/request_permission\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"toolCall\":{{\"toolCallId\":\"c1\",\"title\":\"Run it\",\"kind\":\"{kind}\"}},\"options\":{AllowAndRejectOptions}}}}}");

    /// <summary>A resolver whose decision the test releases, which records how often and for which request it was asked.</summary>
    private sealed class GateResolver : IPermissionResolver
    {
        private readonly TaskCompletionSource<PermissionDecision> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets how many times a decision was asked for.</summary>
        public int Calls { get; private set; }

        /// <summary>Gets the request the last decision was asked for.</summary>
        public PermissionRequestEvent? Request { get; private set; }

        /// <summary>Waits until a decision has been asked for, or the timeout or cancellation ends the wait.</summary>
        /// <param name="timeout">The longest time to wait.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        public Task WaitForEntryAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            _entered.Task.WaitAsync(timeout, cancellationToken);

        /// <summary>Releases the pending decision.</summary>
        /// <param name="decision">The decision to deliver.</param>
        public void Answer(PermissionDecision decision) => _answer.TrySetResult(decision);

        /// <inheritdoc />
        public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            Calls++;
            Request = request;
            _entered.TrySetResult();
            return _answer.Task.WaitAsync(cancellationToken);
        }
    }
}
