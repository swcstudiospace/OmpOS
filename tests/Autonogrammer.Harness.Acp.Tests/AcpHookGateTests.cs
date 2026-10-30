using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// What the hook gate sees and decides: a hook may deny before the approval policy, the policy still bounds every grant, a denied read answers with an
/// error, a denied prompt sends nothing, observations are aggregated, a follow-up continues the stream, and a session without a gate behaves as before.
/// </summary>
public sealed class AcpHookGateTests
{
    private const long PermissionRequestId = 910;
    private const long FsRequestId = 911;
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private const string AllowAndRejectOptions =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"},{\"optionId\":\"reject-once\",\"name\":\"Reject\",\"kind\":\"reject_once\"}]";

    /// <summary>A hook that denies a permission request answers the agent with the reject option before the approval policy or the resolver is consulted.</summary>
    [Fact]
    public async Task ADeniedPermissionIsRejectedBeforeThePolicyAndTheResolverAreAsked()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new RecordingHookGate { OnPermission = static _ => new AcpHookVerdict { Denied = true, UserMessage = "no force pushes" } };
        var resolver = new CountingResolver();
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        var options = new AcpClientOptions
        {
            WorkingDirectory = AcpFixtures.CapturedCwd,
            AuthMethodId = "agent",
            ApprovalPolicy = new AcpApprovalPolicy(["execute"]),
            HookGate = gate,
        };
        var session = new AcpAgentSession(transport, options, resolver, TimeSpan.FromSeconds(30));
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest());

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("reject-once");
        resolver.Calls.ShouldBe(0);
        gate.Permissions.ShouldHaveSingleItem().CallId.ShouldBe("c1");
        gate.Permissions[0].Kind.ShouldBe("execute");
    }

    /// <summary>A hook that allows a request cannot grant it: the policy has the last word, and a policy that denies everything still rejects.</summary>
    [Fact]
    public async Task AHookCannotGrantWhatThePolicyDenies()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new RecordingHookGate();
        var resolver = new CountingResolver();
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        var options = new AcpClientOptions
        {
            WorkingDirectory = AcpFixtures.CapturedCwd,
            AuthMethodId = "agent",
            ApprovalPolicy = AcpApprovalPolicy.DenyAll,
            HookGate = gate,
        };
        var session = new AcpAgentSession(transport, options, resolver, TimeSpan.FromSeconds(30));
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest());

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("reject-once");
        gate.Permissions.Count.ShouldBe(1);
        resolver.Calls.ShouldBe(0);
    }

    /// <summary>A gate that throws is treated as an allow: the request then follows the policy, which here denies it by default.</summary>
    [Fact]
    public async Task AFailingGateDoesNotStopThePolicyFromDeciding()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new RecordingHookGate { OnPermission = static _ => throw new InvalidOperationException("boom") };
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        var options = new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent", HookGate = gate };
        await using var session = NewSession(transport, options);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest());

        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("reject-once");
    }

    /// <summary>Without a gate the permission path is what it always was: the policy admits a kind, the resolver decides.</summary>
    [Fact]
    public async Task WithoutAGateTheResolverStillDecidesWhatThePolicyAdmits()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolver = new CountingResolver();
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = RecordingTransport(answered);
        var options = new AcpClientOptions
        {
            WorkingDirectory = AcpFixtures.CapturedCwd,
            AuthMethodId = "agent",
            ApprovalPolicy = new AcpApprovalPolicy(["execute"]),
        };
        var session = new AcpAgentSession(transport, options, resolver, TimeSpan.FromSeconds(30));
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest());

        await resolver.Entered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        resolver.Calls.ShouldBe(1);
    }

    /// <summary>A denied read answers the agent with an error that names the hook and never sends the file's content; an allowed read hands the gate the whole file.</summary>
    [Fact]
    public async Task ADeniedReadAnswersWithAnErrorAndAnAllowedReadSeesTheWholeFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("acp-hook-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "secret.txt"), "line one\nline two", ct).ConfigureAwait(true);
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "open.txt"), "hello\nworld", ct).ConfigureAwait(true);
            var gate = new RecordingHookGate
            {
                OnReadFile = static (path, _) => path.EndsWith("secret.txt", StringComparison.Ordinal)
                    ? new AcpHookVerdict { Denied = true, AgentMessage = "secrets stay private" }
                    : AcpHookVerdict.Allow,
            };

            var denied = await AnswerFsAsync(gate, root.FullName, FsReadRequest("secret.txt"), ct).ConfigureAwait(true);
            denied["error"]!["code"]!.GetValue<long>().ShouldBe(-32602);
            denied["error"]!["message"]!.GetValue<string>().ShouldContain("denied by a hook");
            denied["error"]!["message"]!.GetValue<string>().ShouldContain("secrets stay private");
            denied["result"].ShouldBeNull();

            var allowed = await AnswerFsAsync(gate, root.FullName, FsReadRequest("open.txt"), ct).ConfigureAwait(true);
            allowed["result"]!["content"]!.GetValue<string>().ShouldBe("hello\nworld");
            gate.Reads.Select(read => read.Content).ShouldContain("hello\nworld", StringComparer.Ordinal);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>A write reports the previous text and the new text to the gate after the file was written.</summary>
    [Fact]
    public async Task AWriteTellsTheGateWhatChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("acp-hook-");
        try
        {
            var path = Path.Combine(root.FullName, "a.txt");
            await File.WriteAllTextAsync(path, "old text", ct).ConfigureAwait(true);
            var gate = new RecordingHookGate();

            var answer = await AnswerFsAsync(gate, root.FullName, FsWriteRequest("a.txt", "new text"), ct).ConfigureAwait(true);

            answer["error"].ShouldBeNull();
            (await File.ReadAllTextAsync(path, ct).ConfigureAwait(true)).ShouldBe("new text");
            var write = gate.Writes.ShouldHaveSingleItem();
            write.Path.ShouldBe(path);
            write.Previous.ShouldBe("old text");
            write.Content.ShouldBe("new text");

            await AnswerFsAsync(gate, root.FullName, FsWriteRequest("fresh/b.txt", "created"), ct).ConfigureAwait(true);
            gate.Writes[^1].Previous.ShouldBeNull();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>A hook that stops a prompt keeps it from being sent: the turn ends at once with a refusal and a diagnostic, and no prompt frame is written.</summary>
    [Fact]
    public async Task ADeniedPromptSendsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new RecordingHookGate { OnPrompt = static _ => new AcpHookVerdict { Denied = true, UserMessage = "not on a Friday" } };
        var peer = new ScriptedOmpPeer();
        var transport = new ScriptedAcpTransport(peer.Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        await using var session = NewSession(transport, Options(gate));
        await session.CreateAsync(ct).ConfigureAwait(true);

        var events = await CollectAsync(session.SubmitPromptAsync("deploy", new PromptOptions(), ct), ct).ConfigureAwait(true);

        events.Count.ShouldBe(2);
        events[0].ShouldBeOfType<DiagnosticEvent>().Message.ShouldContain("not on a Friday");
        events[1].ShouldBeOfType<CompletedEvent>().Reason.ShouldBe("refusal");
        transport.Sent.Any(frame => string.Equals(AcpFixtures.MethodOf(frame), "session/prompt", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>The context the hooks added is put in front of the prompt text that is sent.</summary>
    [Fact]
    public async Task HookContextIsPutInFrontOfThePrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new RecordingHookGate { OnPrompt = static _ => new AcpHookVerdict { PromptContext = "Team rule: use tabs." } };
        var peer = new ScriptedOmpPeer();
        var transport = new ScriptedAcpTransport(peer.Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        await using var session = NewSession(transport, Options(gate));
        await session.CreateAsync(ct).ConfigureAwait(true);

        var turn = StartTurnAsync(session, ct);
        await WaitForPromptsAsync(transport, 1, ct).ConfigureAwait(true);
        transport.Deliver(peer.EndPrompt("end_turn"));
        await DrainAsync(turn, ct).ConfigureAwait(true);

        var frame = JsonNode.Parse(transport.Sent.Single(f => string.Equals(AcpFixtures.MethodOf(f), "session/prompt", StringComparison.Ordinal)))!;
        frame["params"]!["prompt"]![0]!["text"]!.GetValue<string>().ShouldBe("Team rule: use tabs.\n\nwork");
        gate.Prompts.ShouldBe(["work"]);
    }

    /// <summary>
    /// Thinking and message chunks are aggregated until the block ends, tool calls and their updates pass through with their fields, and the gate
    /// hears the stop reason of the turn.
    /// </summary>
    [Fact]
    public async Task UpdatesAreAggregatedAndReportedInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new RecordingHookGate();
        var peer = new ScriptedOmpPeer();
        var transport = new ScriptedAcpTransport(peer.Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        await using var session = NewSession(transport, Options(gate));
        await session.CreateAsync(ct).ConfigureAwait(true);
        var turn = StartTurnAsync(session, ct);
        await WaitForPromptsAsync(transport, 1, ct).ConfigureAwait(true);

        transport.Deliver(Update("{\"sessionUpdate\":\"agent_thought_chunk\",\"content\":{\"type\":\"text\",\"text\":\"think \"}}"));
        transport.Deliver(Update("{\"sessionUpdate\":\"agent_thought_chunk\",\"content\":{\"type\":\"text\",\"text\":\"hard\"}}"));
        transport.Deliver(Update("{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"Hello \"}}"));
        transport.Deliver(Update("{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"world\"}}"));
        transport.Deliver(Update("{\"sessionUpdate\":\"tool_call\",\"toolCallId\":\"t1\",\"title\":\"Run ls\",\"kind\":\"execute\",\"status\":\"pending\",\"rawInput\":{\"command\":\"ls\"}}"));
        transport.Deliver(Update("{\"sessionUpdate\":\"tool_call_update\",\"toolCallId\":\"t1\",\"status\":\"completed\",\"content\":[{\"type\":\"content\",\"content\":{\"type\":\"text\",\"text\":\"a.txt\"}}]}"));
        transport.Deliver(Update("{\"sessionUpdate\":\"plan\",\"entries\":[{\"content\":\"step\",\"status\":\"in_progress\"}]}"));
        transport.Deliver(peer.EndPrompt("end_turn"));
        await DrainAsync(turn, ct).ConfigureAwait(true);

        var signals = gate.Signals;
        signals.Select(signal => signal.Kind).ShouldBe(
        [
            AcpHookSignalKind.AgentThought,
            AcpHookSignalKind.AgentResponse,
            AcpHookSignalKind.ToolCall,
            AcpHookSignalKind.ToolCallUpdate,
            AcpHookSignalKind.Plan,
        ]);
        signals[0].Text.ShouldBe("think hard");
        signals[0].Duration.ShouldNotBeNull();
        signals[1].Text.ShouldBe("Hello world");
        signals[2].ToolCallId.ShouldBe("t1");
        signals[2].ToolKind.ShouldBe("execute");
        signals[2].RawInput.ShouldNotBeNull().GetProperty("command").GetString().ShouldBe("ls");
        signals[3].Status.ShouldBe("completed");
        signals[3].Text.ShouldBe("a.txt");
        signals[4].Text.ShouldBe("- [~] step");
        gate.StopReasons.ShouldBe(["end_turn"]);
    }

    /// <summary>A stop hook that asks for a follow-up continues the same stream: the notice is emitted, a second prompt is sent, and only the last turn's completion ends the stream.</summary>
    [Fact]
    public async Task AFollowUpContinuesTheStream()
    {
        var ct = TestContext.Current.CancellationToken;
        var calls = 0;
        var gate = new RecordingHookGate
        {
            OnTurnEnded = _ => ++calls == 1
                ? new AcpTurnEndDecision { FollowUpPrompt = "now run the tests", Notice = "Hook continued the conversation (1 of 5)" }
                : AcpTurnEndDecision.End,
        };
        var peer = new ScriptedOmpPeer();
        var transport = new ScriptedAcpTransport(peer.Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        await using var session = NewSession(transport, Options(gate));
        await session.CreateAsync(ct).ConfigureAwait(true);
        var collected = new List<HarnessEvent>();
        var turn = Task.Run(async () =>
        {
            await foreach (var harnessEvent in session.SubmitPromptAsync("work", new PromptOptions(), ct).ConfigureAwait(false))
            {
                collected.Add(harnessEvent);
            }
        }, ct);

        await WaitForPromptsAsync(transport, 1, ct).ConfigureAwait(true);
        transport.Deliver(peer.EndPrompt("end_turn"));
        await WaitForPromptsAsync(transport, 2, ct).ConfigureAwait(true);
        transport.Deliver(peer.EndPrompt("end_turn"));
        await turn.WaitAsync(WaitLimit, ct).ConfigureAwait(true);

        var prompts = transport.Sent.Where(f => string.Equals(AcpFixtures.MethodOf(f), "session/prompt", StringComparison.Ordinal)).Select(f => JsonNode.Parse(f)!["params"]!["prompt"]![0]!["text"]!.GetValue<string>()).ToArray();
        prompts.ShouldBe(["work", "now run the tests"]);
        collected.OfType<DiagnosticEvent>().Single().Message.ShouldBe("Hook continued the conversation (1 of 5)");
        collected.OfType<CompletedEvent>().ShouldHaveSingleItem();
        collected[^1].ShouldBeOfType<CompletedEvent>();
        gate.StopReasons.ShouldBe(["end_turn", "end_turn"]);
    }

    /// <summary>The gate hears that the session opened (new, not resumed) and, when the session is disposed, that it closed.</summary>
    [Fact]
    public async Task TheGateHearsTheLifecycleOfTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new RecordingHookGate();
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = NewSession(transport, Options(gate));

        await session.CreateAsync(ct).ConfigureAwait(true);
        gate.Opened.ShouldBe([(AcpFixtures.CapturedSessionId, false)]);
        gate.Closed.ShouldBeEmpty();

        await session.DisposeAsync().ConfigureAwait(true);
        gate.Closed.ShouldBe(["user_close"]);
    }

    private static AcpClientOptions Options(IAcpHookGate gate) =>
        new() { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent", HookGate = gate };

    private static byte[] Update(string update) =>
        AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"update\":{update}}}}}");

    private static byte[] PermissionRequest() =>
        AcpFixtures.Utf8(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{PermissionRequestId},\"method\":\"session/request_permission\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"toolCall\":{{\"toolCallId\":\"c1\",\"title\":\"git push --force\",\"kind\":\"execute\",\"rawInput\":{{\"command\":\"git push --force\"}}}},\"options\":{AllowAndRejectOptions}}}}}");

    private static ScriptedAcpTransport RecordingTransport(TaskCompletionSource<byte[]> answered) =>
        new(frame =>
        {
            if (AcpFixtures.MethodOf(frame) is null && AcpFixtures.IdOf(frame) == PermissionRequestId)
            {
                answered.TrySetResult(frame);
            }

            return AcpFixtures.Reply(frame);
        });

    private static byte[] FsReadRequest(string path) =>
        AcpFixtures.Utf8(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = FsRequestId,
            ["method"] = "fs/read_text_file",
            ["params"] = new JsonObject { ["sessionId"] = AcpFixtures.CapturedSessionId, ["path"] = path },
        }.ToJsonString());

    private static byte[] FsWriteRequest(string path, string content) =>
        AcpFixtures.Utf8(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = FsRequestId,
            ["method"] = "fs/write_text_file",
            ["params"] = new JsonObject { ["sessionId"] = AcpFixtures.CapturedSessionId, ["path"] = path, ["content"] = content },
        }.ToJsonString());

    private static AcpAgentSession NewSession(IAcpTransport transport, AcpClientOptions options) => new(transport, options);

    private static async Task<JsonNode> AnswerFsAsync(IAcpHookGate gate, string root, byte[] request, CancellationToken ct)
    {
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (AcpFixtures.MethodOf(frame) is null && AcpFixtures.IdOf(frame) == FsRequestId)
            {
                answered.TrySetResult(frame);
            }

            return AcpFixtures.Reply(frame);
        });
        await using var transportScope = transport.ConfigureAwait(true);
        var options = new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent", FsRoot = root, HookGate = gate };
        var session = NewSession(transport, options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(request);
        var answer = await answered.Task.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        return JsonNode.Parse(answer)!;
    }

    private static Task<List<HarnessEvent>> StartTurnAsync(AcpAgentSession session, CancellationToken ct) =>
        CollectAsync(session.SubmitPromptAsync("work", new PromptOptions(), ct), ct);

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> stream, CancellationToken ct)
    {
        // Run on a pool thread so the enumeration does not depend on the caller's context while the test drives the transport.
        return await Task.Run(
            async () =>
            {
                var events = new List<HarnessEvent>();
                await foreach (var harnessEvent in stream.WithCancellation(ct).ConfigureAwait(false))
                {
                    events.Add(harnessEvent);
                }

                return events;
            },
            ct).ConfigureAwait(true);
    }

    private static async Task DrainAsync(Task<List<HarnessEvent>> turn, CancellationToken ct) =>
        _ = await turn.WaitAsync(WaitLimit, ct).ConfigureAwait(true);

    private static async Task WaitForPromptsAsync(ScriptedAcpTransport transport, int count, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(WaitLimit);
        while (transport.Sent.Count(frame => string.Equals(AcpFixtures.MethodOf(frame), "session/prompt", StringComparison.Ordinal)) < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), limit.Token).ConfigureAwait(true);
        }
    }

    /// <summary>A resolver that counts the requests it was asked about and signals when the first arrives. Its decision never arrives.</summary>
    private sealed class CountingResolver : IPermissionResolver
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _calls);
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return PermissionDecision.Deny;
        }
    }
}
