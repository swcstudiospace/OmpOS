using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>Prompt admission, streaming, and terminal correlation against a scripted engine.</summary>
public sealed class RpcPromptStreamTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>An admitted prompt streams its deltas in order and completes on its <c>prompt_result</c>.</summary>
    [Fact]
    public async Task AdmittedPromptStreamsDeltasThenCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        await RunAsync(
            (command, id) => command switch
            {
                "prompt" =>
                [
                    RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""),
                    RpcTestData.TextDelta("Hel"),
                    RpcTestData.TextDelta("lo"),
                    RpcTestData.PromptResult(id, "completed"),
                ],
                _ => null,
            },
            async session =>
            {
                List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct)).ConfigureAwait(true);

                events.ShouldBe([new TextDeltaEvent("Hel"), new TextDeltaEvent("lo"), new CompletedEvent("completed")]);
                session.State.ShouldBe(HarnessState.Ready);
            },
            ct);
    }

    /// <summary>A local slash command answers <c>agentInvoked: false</c> and completes with no <c>prompt_result</c>.</summary>
    [Fact]
    public async Task LocalCommandCompletesWithoutPromptResult()
    {
        var ct = TestContext.Current.CancellationToken;
        await RunAsync(
            (command, id) => command switch
            {
                "prompt" => [RpcTestData.Ok(id, "prompt", """{"agentInvoked":false}""")],
                _ => null,
            },
            async session =>
            {
                List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("/help", new PromptOptions(), ct)).ConfigureAwait(true);

                events.ShouldBe([new CompletedEvent("completed")]);
                session.State.ShouldBe(HarnessState.Ready);
            },
            ct);
    }

    /// <summary>An error terminal surfaces its message and retryability, and nothing follows it: the turn ends with the error alone.</summary>
    [Fact]
    public async Task ErrorStatusEmitsErrorAloneAndNoCompletion()
    {
        var ct = TestContext.Current.CancellationToken;
        await RunAsync(
            (command, id) => command switch
            {
                "prompt" =>
                [
                    RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""),
                    RpcTestData.PromptResult(id, "error", """{"message":"rate limited","retryable":true}"""),
                ],
                _ => null,
            },
            async session =>
            {
                List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct)).ConfigureAwait(true);

                events.ShouldBe([new ErrorEvent("rate limited", true)]);
                session.State.ShouldBe(HarnessState.Ready);
            },
            ct);
    }

    /// <summary>A dropped oversized frame is a non-terminal diagnostic: the prompt still streams and completes on its prompt_result.</summary>
    [Fact]
    public async Task DroppedOversizedFrameIsDiagnosticAndTurnStillCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        await RunAsync(
            (command, id) => command switch
            {
                "prompt" =>
                [
                    RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""),
                    """{"type":"rpc_frame_error","originalType":"message_update","error":"transport limit exceeded"}""",
                    RpcTestData.TextDelta("done"),
                    RpcTestData.PromptResult(id, "completed"),
                ],
                _ => null,
            },
            async session =>
            {
                List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct)).ConfigureAwait(true);

                events.Count.ShouldBe(3);
                events[0].ShouldBeOfType<DiagnosticEvent>();
                events[1].ShouldBe(new TextDeltaEvent("done"));
                events[2].ShouldBe(new CompletedEvent("completed"));
                session.State.ShouldBe(HarnessState.Ready);
            },
            ct);
    }

    /// <summary>Cancelling an in-flight prompt sends <c>abort</c> and completes the stream as cancelled.</summary>
    [Fact]
    public async Task CancelAbortsInFlightPromptAsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        string? promptId = null;
        var abortSent = false;
        await RunAsync(
            (command, id) =>
            {
                switch (command)
                {
                    case "prompt":
                        promptId = id;
                        return [RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}""")];
                    case "abort":
                        abortSent = true;
                        return [RpcTestData.Ok(id, "abort", "null"), RpcTestData.PromptResult(promptId!, "aborted")];
                    default:
                        return null;
                }
            },
            async session =>
            {
                Task<List<HarnessEvent>> consumer = CollectAsync(session.SubmitPromptAsync("long task", new PromptOptions(), ct));
                await session.CancelAsync(ct).ConfigureAwait(true);
                List<HarnessEvent> events = await consumer.ConfigureAwait(true);

                events.ShouldBe([new CompletedEvent("cancelled")]);
                abortSent.ShouldBeTrue();
                session.State.ShouldBe(HarnessState.Ready);
            },
            ct);
    }

    /// <summary>
    /// A consumer that stops after its first event abandons the prompt. The abort's terminal is drained rather than left
    /// unread, so stopping early returns the session to Ready instead of faulting it on a timeout.
    /// </summary>
    [Fact]
    public async Task StoppingPromptEarlyLeavesSessionReady()
    {
        var ct = TestContext.Current.CancellationToken;
        string? promptId = null;
        await RunAsync(
            (command, id) =>
            {
                switch (command)
                {
                    case "prompt":
                        promptId = id;
                        return [RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""), RpcTestData.TextDelta("Hel"), RpcTestData.TextDelta("lo")];
                    case "abort":
                        return [RpcTestData.Ok(id, "abort", "null"), RpcTestData.PromptResult(promptId!, "aborted")];
                    default:
                        return null;
                }
            },
            async session =>
            {
                IAsyncEnumerator<HarnessEvent> stream = session.SubmitPromptAsync("long task", new PromptOptions(), ct).GetAsyncEnumerator(ct);
                (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
                stream.Current.ShouldBe(new TextDeltaEvent("Hel"));

                await stream.DisposeAsync().ConfigureAwait(true);

                session.State.ShouldBe(HarnessState.Ready);
            },
            ct);
    }

    /// <summary>A <c>prompt_result</c> with an unknown status faults the session and fails the consumer, never hanging it.</summary>
    [Fact]
    public async Task UnknownPromptStatusFaultsSessionAndFailsConsumer()
    {
        var ct = TestContext.Current.CancellationToken;
        await RunAsync(
            (command, id) => command switch
            {
                "prompt" =>
                [
                    RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""),
                    RpcTestData.PromptResult(id, "mystery"),
                ],
                _ => null,
            },
            async session =>
            {
                await Should.ThrowAsync<MalformedFrameHarnessException>(
                    () => CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct))).ConfigureAwait(true);
                session.State.ShouldBe(HarnessState.Faulted);
            },
            ct);
    }

    /// <summary>
    /// Starts a session against a scripted engine. The engine answers the handshake itself and every other command
    /// with <paramref name="scripted"/>. Runs <paramref name="body"/>, then disposes the session.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The OmpRpcSession takes ownership of the transport and disposes it in DisposeAsync.")]
    private static async Task RunAsync(
        Func<string, string, IEnumerable<string>?> scripted,
        Func<OmpRpcSession, Task> body,
        CancellationToken ct)
    {
        FakeRpcTransport transport = Engine(scripted);
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);
        await body(session).ConfigureAwait(true);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    private static FakeRpcTransport Engine(Func<string, string, IEnumerable<string>?> scripted)
    {
        var transport = new FakeRpcTransport(line =>
        {
            string command = RpcTestData.TypeOf(line);
            string id = RpcTestData.IdOf(line);
            return Handshake(command, id) ?? scripted(command, id) ?? Array.Empty<string>();
        });
        transport.Emit(ReadyLine);
        return transport;
    }

    private static IEnumerable<string>? Handshake(string command, string id) => command switch
    {
        "negotiate_protocol" => [RpcTestData.Ok(id, command, """{"protocolVersion":2}""")],
        "get_state" => [RpcTestData.Ok(id, command, RpcTestData.StateData)],
        _ => null,
    };

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var collected = new List<HarnessEvent>();
        IAsyncEnumerator<HarnessEvent> enumerator = events.GetAsyncEnumerator();
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(true))
            {
                collected.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(true);
        }

        return collected;
    }
}
