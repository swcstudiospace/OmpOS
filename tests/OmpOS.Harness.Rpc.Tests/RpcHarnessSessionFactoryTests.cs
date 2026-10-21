using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>The RPC harness factory: lazy start, scripted event mapping, and the pinned start failure path.</summary>
/// <remarks>Engines are scripted at the transport boundary. No omp process is launched, except for the missing-binary case, which never spawns.</remarks>
public sealed class RpcHarnessSessionFactoryTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>A scripted engine stream maps to harness events in the order the engine emitted them.</summary>
    [Fact]
    public async Task ScriptedStreamMapsToHarnessEventsInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var starter = new ScriptedStarter((command, id) => command switch
        {
            "prompt" =>
            [
                RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""),
                RpcTestData.TextDelta("Hel"),
                ThinkingDelta("hmm"),
                ToolStart("t1", "read"),
                ToolEnd("t1"),
                RpcTestData.TextDelta("lo"),
                RpcTestData.PromptResult(id, "completed"),
            ],
            _ => null,
        });

        IOmpSession session = new RpcHarnessSessionFactory(starter).CreateSession("hi");
        await using (session.ConfigureAwait(false))
        {
            await session.CreateAsync(ct);
            List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct));

            events.Count.ShouldBe(6);
            events[0].ShouldBe(new TextDeltaEvent("Hel"));
            events[1].ShouldBe(new ThoughtEvent("hmm"));
            ToolCallEvent call = events[2].ShouldBeOfType<ToolCallEvent>();
            call.CallId.ShouldBe("t1");
            call.Name.ShouldBe("read");
            ToolCallUpdateEvent update = events[3].ShouldBeOfType<ToolCallUpdateEvent>();
            update.CallId.ShouldBe("t1");
            update.Status.ShouldBe("completed");
            events[4].ShouldBe(new TextDeltaEvent("lo"));
            events[5].ShouldBe(new CompletedEvent("completed"));
            session.State.ShouldBe(HarnessState.Ready);
        }
    }

    /// <summary>Creating the session spawns nothing; the engine starts on the first <c>CreateAsync</c>.</summary>
    [Fact]
    public async Task CreateSessionDoesNotStartEngineUntilCreateAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var starter = new ScriptedStarter(static (_, _) => null);

        IOmpSession session = new RpcHarnessSessionFactory(starter).CreateSession("hi");

        starter.Starts.ShouldBe(0);
        session.State.ShouldBe(HarnessState.Disconnected);
        await using (session.ConfigureAwait(false))
        {
            await session.CreateAsync(ct);

            starter.Starts.ShouldBe(1);
            session.State.ShouldBe(HarnessState.Ready);
        }
    }

    /// <summary>A prompt submitted before <c>CreateAsync</c> is refused with a state error, and no engine starts.</summary>
    [Fact]
    public async Task SubmitBeforeCreateAsyncIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var starter = new ScriptedStarter(static (_, _) => null);
        IOmpSession session = new RpcHarnessSessionFactory(starter).CreateSession("hi");

        await using (session.ConfigureAwait(false))
        {
            await Should.ThrowAsync<InvalidHarnessTransitionException>(
                async () => await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct)).ConfigureAwait(false));
            starter.Starts.ShouldBe(0);
        }
    }

    /// <summary><c>ResumeAsync</c> starts the engine and switches it to the given session file.</summary>
    [Fact]
    public async Task ResumeAsyncStartsEngineAndSwitchesToSessionFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var starter = new ScriptedStarter(static (_, _) => null);
        IOmpSession session = new RpcHarnessSessionFactory(starter).CreateSession("hi");

        await using (session.ConfigureAwait(false))
        {
            await session.ResumeAsync("/sessions/s1.jsonl", ct);

            session.State.ShouldBe(HarnessState.Ready);
            starter.Starts.ShouldBe(1);
            starter.LastTransport.ShouldNotBeNull().Sent.Select(RpcTestData.TypeOf).ShouldContain("switch_session", StringComparer.Ordinal);
        }
    }

    /// <summary>A session starts at most once: a second <c>CreateAsync</c> is refused and opens no second engine.</summary>
    [Fact]
    public async Task SecondCreateAsyncIsRefusedWithoutStartingAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var starter = new ScriptedStarter(static (_, _) => null);
        IOmpSession session = new RpcHarnessSessionFactory(starter).CreateSession("hi");

        await using (session.ConfigureAwait(false))
        {
            await session.CreateAsync(ct);

            await Should.ThrowAsync<InvalidHarnessTransitionException>(
                async () => await session.CreateAsync(ct).ConfigureAwait(false));
            starter.Starts.ShouldBe(1);
        }
    }

    /// <summary>
    /// A missing omp binary fails <c>CreateAsync</c> with the probe's start exception, before any engine is spawned,
    /// and leaves the session faulted. The probe runs through the production <see cref="OmpRpcClient"/> path, so the pin
    /// check cannot be skipped.
    /// </summary>
    [Fact]
    public async Task MissingOmpBinaryFailsCreateAsyncAndFaultsSession()
    {
        var ct = TestContext.Current.CancellationToken;
        string missing = Path.Combine(Path.GetTempPath(), "omp-not-installed-" + Guid.NewGuid().ToString("N"));
        var client = new OmpRpcClient(new OmpRpcClientOptions { Cwd = Path.GetTempPath(), OmpPath = missing });
        IOmpSession session = new RpcHarnessSessionFactory(client).CreateSession("hi");

        await using (session.ConfigureAwait(false))
        {
            session.State.ShouldBe(HarnessState.Disconnected);
            await Should.ThrowAsync<ProcessStartHarnessException>(
                async () => await session.CreateAsync(ct).ConfigureAwait(false));
            session.State.ShouldBe(HarnessState.Faulted);
        }
    }

    /// <summary>Disposing a session that never started leaves it disposed and never starts an engine.</summary>
    [Fact]
    public async Task DisposingUnstartedSessionNeverStartsEngine()
    {
        var starter = new ScriptedStarter(static (_, _) => null);
        IOmpSession session = new RpcHarnessSessionFactory(starter).CreateSession("hi");

        await session.DisposeAsync();

        session.State.ShouldBe(HarnessState.Disposed);
        starter.Starts.ShouldBe(0);
    }

    private static string ThinkingDelta(string delta) =>
        "{\"type\":\"message_update\",\"messageId\":\"m1\",\"message\":{},\"assistantMessageEvent\":{\"type\":\"thinking_delta\",\"contentIndex\":0,\"delta\":\""
        + delta
        + "\"}}";

    private static string ToolStart(string callId, string name) =>
        "{\"type\":\"tool_execution_start\",\"toolCallId\":\"" + callId + "\",\"toolName\":\"" + name + "\"}";

    private static string ToolEnd(string callId) =>
        "{\"type\":\"tool_execution_end\",\"toolCallId\":\"" + callId + "\",\"isError\":false}";

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
        "switch_session" => [RpcTestData.Ok(id, command, """{"cancelled":false}""")],
        _ => null,
    };

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var collected = new List<HarnessEvent>();
        IAsyncEnumerator<HarnessEvent> enumerator = events.GetAsyncEnumerator();
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                collected.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        return collected;
    }

    /// <summary>Opens each engine session over a fresh scripted transport, counting starts. Stands in for the process boundary.</summary>
    private sealed class ScriptedStarter : IRpcSessionStarter
    {
        private readonly Func<string, string, IEnumerable<string>?> _scripted;
        private int _starts;

        public ScriptedStarter(Func<string, string, IEnumerable<string>?> scripted)
        {
            _scripted = scripted;
        }

        public int Starts => Volatile.Read(ref _starts);

        public FakeRpcTransport? LastTransport { get; private set; }

        public Task<IOmpSession> CreateAsync(CancellationToken cancellationToken) => OpenAsync(null, cancellationToken);

        public Task<IOmpSession> ResumeAsync(string sessionPath, CancellationToken cancellationToken) =>
            OpenAsync(sessionPath, cancellationToken);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Ownership of the session transfers to the returned IOmpSession; the factory disposes it.")]
        private async Task<IOmpSession> OpenAsync(string? sessionPath, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _starts);
            FakeRpcTransport transport = Engine(_scripted);
            LastTransport = transport;
            var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            // Ownership of the session transfers to the caller through the returned value; the factory disposes it.
            return await OmpRpcClient.StartSessionAsync(
                session,
                started => sessionPath is null
                    ? started.CreateAsync(cancellationToken)
                    : started.ResumeAsync(sessionPath, cancellationToken)).ConfigureAwait(false);
        }
    }
}
