using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// The answer to an admitted <c>session/request_permission</c> reflects the decision the resolver returns. Only an allow
/// decision grants; a reject decision, a decision that never arrives, a failing resolver and a missing resolver each deny.
/// Sessions come from <see cref="OmpAcpClient"/>, so the resolver and the decision timeout are the ones its constructor received.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
public sealed class AcpPermissionDecisionTests
{
    private const long PermissionRequestId = 901;

    private const string AllowAndRejectOptions =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"},{\"optionId\":\"reject-once\",\"name\":\"Reject\",\"kind\":\"reject_once\"}]";

    private static readonly TimeSpan DecisionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    /// <summary>An approve decision answers the admitted request with the allow option.</summary>
    [Fact]
    public async Task ApproveDecisionAnswersTheRequestWithTheAllowOption()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        var resolver = new FixedResolver(PermissionDecision.Allow);
        await using var session = new OmpAcpClient(_ => transport, resolver, DecisionTimeout).CreateSession(Options());
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest("execute"));

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        OptionIdOf(answer).ShouldBe("allow-once");
        resolver.Calls.ShouldBe(1);
    }

    /// <summary>A deny decision answers the admitted request with the reject option.</summary>
    [Fact]
    public async Task DenyDecisionAnswersTheRequestWithTheRejectOption()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        await using var session = new OmpAcpClient(_ => transport, new FixedResolver(PermissionDecision.Deny), DecisionTimeout).CreateSession(Options());
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest("execute"));

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        OptionIdOf(answer).ShouldBe("reject-once");
    }

    /// <summary>A decision that never arrives is denied once the decision timeout ends the wait.</summary>
    [Fact]
    public async Task NoDecisionWithinTimeoutAnswersTheRequestWithTheRejectOption()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        await using var session = new OmpAcpClient(_ => transport, new SilentResolver(), TimeSpan.FromMilliseconds(200)).CreateSession(Options());
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest("execute"));

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        OptionIdOf(answer).ShouldBe("reject-once");
    }

    /// <summary>With no resolver supplied, an admitted request is denied: the policy alone never grants.</summary>
    [Fact]
    public async Task WithoutResolverAdmittedRequestIsAnsweredWithTheRejectOption()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        await using var session = new OmpAcpClient(_ => transport).CreateSession(Options());
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest("execute"));

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        OptionIdOf(answer).ShouldBe("reject-once");
    }

    /// <summary>A resolver that fails is treated as a denial, and the session keeps answering the agent.</summary>
    [Fact]
    public async Task FailingResolverAnswersTheRequestWithTheRejectOption()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        await using var session = new OmpAcpClient(_ => transport, new FailingResolver(), DecisionTimeout).CreateSession(Options());
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest("execute"));

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        OptionIdOf(answer).ShouldBe("reject-once");
    }

    private static AcpClientOptions Options() => new()
    {
        WorkingDirectory = AcpFixtures.CapturedCwd,
        AuthMethodId = "agent",
        ApprovalPolicy = new AcpApprovalPolicy(["execute"]),
    };

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

    private static string OptionIdOf(byte[] answer) =>
        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>();

    private static byte[] PermissionRequest(string kind) =>
        AcpFixtures.Utf8(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{PermissionRequestId},\"method\":\"session/request_permission\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"toolCall\":{{\"toolCallId\":\"c1\",\"title\":\"Run it\",\"kind\":\"{kind}\"}},\"options\":{AllowAndRejectOptions}}}}}");

    /// <summary>A resolver that answers at once with a fixed decision and counts how often it was asked.</summary>
    private sealed class FixedResolver(PermissionDecision decision) : IPermissionResolver
    {
        /// <summary>Gets how many times a decision was asked for.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(decision);
        }
    }

    /// <summary>A resolver whose decision never arrives: only the decision timeout ends its wait.</summary>
    private sealed class SilentResolver : IPermissionResolver
    {
        /// <inheritdoc />
        public async Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return PermissionDecision.Allow;
        }
    }

    /// <summary>A resolver whose decision source fails.</summary>
    private sealed class FailingResolver : IPermissionResolver
    {
        /// <inheritdoc />
        public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken) =>
            Task.FromException<PermissionDecision>(new InvalidOperationException("The decision source is unavailable."));
    }
}
