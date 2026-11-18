using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// A hook gate that never answers must not stall the pump, and it must not let the action through: past the hook timeout its check counts as a
/// denial, and the agent's next permission request is answered with the reject option, whatever the resolver would grant.
/// </summary>
public sealed class AcpHookGateTimeoutTests
{
    private const long PermissionRequestId = 920;
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    private const string AllowAndRejectOptions =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"},{\"optionId\":\"reject-once\",\"name\":\"Reject\",\"kind\":\"reject_once\"}]";

    /// <summary>A permission check that never returns is denied after the hook timeout: the request is answered with the reject option although the resolver grants it.</summary>
    [Fact]
    public async Task AHookThatNeverAnswersDeniesThePermissionRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (AcpFixtures.MethodOf(frame) is null && AcpFixtures.IdOf(frame) == PermissionRequestId)
            {
                answered.TrySetResult(frame);
            }

            return AcpFixtures.Reply(frame);
        });
        await using var transportScope = transport.ConfigureAwait(true);
        var options = new AcpClientOptions
        {
            WorkingDirectory = AcpFixtures.CapturedCwd,
            AuthMethodId = "agent",
            ApprovalPolicy = new AcpApprovalPolicy(["execute"]),
            HookGate = new NeverAnsweringGate(),
        };
        var session = new AcpAgentSession(transport, options, new AllowingResolver(), TimeSpan.FromSeconds(30));
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest());

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("reject-once");
    }

    private static byte[] PermissionRequest() =>
        AcpFixtures.Utf8(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{PermissionRequestId},\"method\":\"session/request_permission\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"toolCall\":{{\"toolCallId\":\"c1\",\"title\":\"ls\",\"kind\":\"execute\",\"rawInput\":{{\"command\":\"ls\"}}}},\"options\":{AllowAndRejectOptions}}}}}");

    /// <summary>Grants every request it is asked about.</summary>
    private sealed class AllowingResolver : IPermissionResolver
    {
        public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken) =>
            Task.FromResult(PermissionDecision.Allow);
    }

    /// <summary>A gate whose permission check never completes; every other check answers at once.</summary>
    private sealed class NeverAnsweringGate : IAcpHookGate
    {
        private readonly TaskCompletionSource<AcpHookVerdict> _never = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask SessionOpenedAsync(string sessionId, bool resumed, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask<AcpHookVerdict> BeforePromptAsync(string prompt, CancellationToken cancellationToken) => ValueTask.FromResult(AcpHookVerdict.Allow);

        public ValueTask<AcpHookVerdict> BeforePermissionAsync(AcpPermissionRequest request, CancellationToken cancellationToken) => new(_never.Task);

        public ValueTask<AcpHookVerdict> BeforeReadFileAsync(string fullPath, string content, CancellationToken cancellationToken) =>
            ValueTask.FromResult(AcpHookVerdict.Allow);

        public ValueTask AfterFileWriteAsync(string fullPath, string? previousContent, string content, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public void Observe(AcpHookSignal signal) => _ = signal;

        public ValueTask<AcpTurnEndDecision> TurnEndedAsync(string stopReason, CancellationToken cancellationToken) => ValueTask.FromResult(AcpTurnEndDecision.End);

        public ValueTask SessionClosedAsync(string reason, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
