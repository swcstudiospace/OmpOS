using System.Net;
using System.Text;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.InMemory;
using Shouldly;
using Xunit;

namespace OmpOS.EngineHost.Tests;

/// <summary>
/// Engine-host selection, the in-process happy path, remote SSE resume, no-op shell defaults,
/// and descriptor honesty. No test spawns omp: the in-process path uses the scripted in-memory
/// adapter and the remote path uses a scripted <see cref="HttpMessageHandler"/>.
/// </summary>
public sealed class EngineHostTests
{
    /// <summary>The in-memory host reports the InProcess kind by default.</summary>
    [Fact]
    public async Task InProcessHostReportsInProcessKindByDefault()
    {
        await using var host = InProcessEngineHost.ForInMemory(new InMemoryOmpAdapter(Scripted([])));

        host.Descriptor.Kind.ShouldBe(EngineHostKind.InProcess);
    }

    [Fact]
    public void RemoteHostReportsRemoteKindByDefault()
    {
        using var handler = new ScriptedApiHandler();
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        host.Descriptor.Kind.ShouldBe(EngineHostKind.Remote);
    }

    /// <summary>The in-process happy path streams the adapter script through the host.</summary>
    [Fact]
    public async Task InProcessHappyPathStreamsAdapterScript()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = new HarnessEvent[]
        {
            new TextDeltaEvent("Hel"),
            new TextDeltaEvent("lo"),
            new CompletedEvent("end_turn"),
        };
        await using var host = InProcessEngineHost.ForInMemory(new InMemoryOmpAdapter(Scripted(script)));

        var seen = new List<HarnessEvent>();
        await foreach (var evt in host.SubmitPromptAsync("prompt", new PromptOptions(), ct))
        {
            seen.Add(evt);
        }

        seen.ShouldBe(script);
    }

    /// <summary>
    /// The remote host streams events 1..5 and survives the connection drop. <c>ReconnectAsync</c> fetches the
    /// missed events 6..12 (the replay overlaps Seq 5 to prove de-duplication) and holds them. <c>TakeHeldEvents</c>
    /// hands them out in Seq order straight after the resume, without waiting for a prompt.
    /// </summary>
    [Fact]
    public async Task ResumeHoldsMissedEventsUntilTakenInSeqOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedApiHandler();
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);
        var raised = 0;
        host.MissedEventsHeld += (_, _) => raised++;

        var seen = await CollectAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct));
        seen.Count.ShouldBe(5);

        await host.ReconnectAsync(ct);
        raised.ShouldBe(1);

        var missed = host.TakeHeldEvents();
        missed.OfType<TextDeltaEvent>().Select(static evt => evt.Text).ShouldBe(["t6", "t7", "t8", "t9", "t10", "t11"]);
        missed[^1].ShouldBeOfType<CompletedEvent>().Reason.ShouldBe("end_turn");
        missed.Count.ShouldBe(7);
        host.TakeHeldEvents().ShouldBeEmpty();
    }

    /// <summary>Events handed out by <c>TakeHeldEvents</c> are not delivered again by the next prompt.</summary>
    [Fact]
    public async Task NextPromptDoesNotRedeliverTakenMissedEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedApiHandler();
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        _ = await CollectAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct));
        await host.ReconnectAsync(ct);
        host.TakeHeldEvents().Count.ShouldBe(7);

        var next = await CollectAsync(host.SubmitPromptAsync("next prompt", new PromptOptions(), ct));

        next.ShouldBeEmpty();
        handler.ResumePositions.ShouldBe(["5", "12"]);
    }

    /// <summary>An <c>error</c> ends the turn: a completion frame that follows it on the wire is never delivered.</summary>
    [Fact]
    public async Task ErrorEndsTurnAndTrailingCompletionIsNotDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new SingleTurnHandler(
            "id: 1\nevent: text_delta\ndata: {\"kind\":\"text_delta\",\"delta\":\"a\"}\n\n",
            "id: 2\nevent: error\ndata: {\"kind\":\"error\",\"message\":\"boom\",\"retryable\":false}\n\n",
            "id: 3\nevent: completed\ndata: {\"kind\":\"completed\",\"reason\":\"end_turn\"}\n\n");
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        var seen = await CollectAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct));

        seen.Count.ShouldBe(2);
        seen[0].ShouldBeOfType<TextDeltaEvent>().Text.ShouldBe("a");
        seen[1].ShouldBe(new ErrorEvent("boom", false));
    }

    /// <summary>A diagnostic is non-terminal: the turn continues past it and the diagnostic is delivered in order.</summary>
    [Fact]
    public async Task DiagnosticDoesNotEndTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new SingleTurnHandler(
            "id: 1\nevent: text_delta\ndata: {\"kind\":\"text_delta\",\"delta\":\"a\"}\n\n",
            "id: 2\nevent: diagnostic\ndata: {\"kind\":\"diagnostic\",\"message\":\"slow decode\"}\n\n",
            "id: 3\nevent: text_delta\ndata: {\"kind\":\"text_delta\",\"delta\":\"b\"}\n\n",
            "id: 4\nevent: completed\ndata: {\"kind\":\"completed\",\"reason\":\"end_turn\"}\n\n");
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        var seen = await CollectAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct));

        seen.Count.ShouldBe(4);
        seen[1].ShouldBe(new DiagnosticEvent("slow decode"));
        seen[2].ShouldBe(new TextDeltaEvent("b"));
        seen[3].ShouldBe(new CompletedEvent("end_turn"));
    }

    /// <summary>
    /// A second prompt streams only its own reply. The stream starts after the delivered cursor (exclusive), so the
    /// first turn's completion, which the server would replay from the start, cannot end the second turn early.
    /// </summary>
    [Fact]
    public async Task SecondPromptReceivesOnlyItsOwnReply()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new TurnServerHandler();
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        var first = await CollectAsync(host.SubmitPromptAsync("one", new PromptOptions(), ct));
        var second = await CollectAsync(host.SubmitPromptAsync("two", new PromptOptions(), ct));

        first.OfType<TextDeltaEvent>().Select(static evt => evt.Text).ShouldBe(["a"]);
        second.OfType<TextDeltaEvent>().Select(static evt => evt.Text).ShouldBe(["b"]);
        second.OfType<CompletedEvent>().Count().ShouldBe(1);
        handler.ResumePositions.ShouldBe(["2"]);
    }

    /// <summary>A faulted engine that the host replaces is disposed; the replacement serves the next prompt.</summary>
    [Fact]
    public async Task FaultedEngineIsDisposedWhenReplaced()
    {
        var ct = TestContext.Current.CancellationToken;
        var engines = new List<FakeOmpSession>();
        await using var host = new InProcessEngineHost(
            () =>
            {
                var engine = new FakeOmpSession();
                engines.Add(engine);
                return engine;
            },
            new EngineHostDescriptor { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None });

        (await CollectAsync(host.SubmitPromptAsync("one", new PromptOptions(), ct))).Count.ShouldBe(1);
        engines[0].State = HarnessState.Faulted;

        (await CollectAsync(host.SubmitPromptAsync("two", new PromptOptions(), ct))).Count.ShouldBe(1);
        engines.Count.ShouldBe(2);
        engines[0].DisposeCount.ShouldBe(1);
        engines[1].DisposeCount.ShouldBe(0);
    }

    /// <summary>Reconnecting before any prompt throws: there is no session to resume.</summary>
    [Fact]
    public void ReconnectWithoutSessionThrows()
    {
        using var handler = new ScriptedApiHandler();
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        Should.Throw<InvalidOperationException>(() => host.ReconnectAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Cancelling a host with no active prompt completes without throwing.</summary>
    [Fact]
    public async Task CancelWithoutPromptCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var inProcess = InProcessEngineHost.ForInMemory(new InMemoryOmpAdapter(Scripted([])));
        using var handler = new ScriptedApiHandler();
        using var http = ClientFor(handler);
        var remote = new RemoteEngineHost(http);

        await inProcess.CancelAsync(ct);
        await remote.CancelAsync(ct);
    }

    /// <summary>Both hosts report exactly the descriptor they were constructed with.</summary>
    [Fact]
    public async Task DescriptorsAreHonest()
    {
        var inProcessDescriptor = new EngineHostDescriptor
        {
            Kind = EngineHostKind.InProcess,
            Isolation = Isolation.Enclave,
            Attestation = Attestation.Quote,
        };
        var remoteDescriptor = new EngineHostDescriptor
        {
            Kind = EngineHostKind.Remote,
            Isolation = Isolation.Container,
            Attestation = Attestation.None,
        };
        await using var inProcess = InProcessEngineHost.ForInMemory(new InMemoryOmpAdapter(Scripted([])), inProcessDescriptor);
        using var handler = new ScriptedApiHandler();
        using var http = ClientFor(handler);
        var remote = new RemoteEngineHost(http, remoteDescriptor);

        inProcess.Descriptor.ShouldBe(inProcessDescriptor);
        inProcess.Descriptor.Isolation.ShouldBe(Isolation.Enclave);
        inProcess.Descriptor.Attestation.ShouldBe(Attestation.Quote);
        remote.Descriptor.ShouldBe(remoteDescriptor);
        remote.Descriptor.Isolation.ShouldBe(Isolation.Container);
        remote.Descriptor.Attestation.ShouldBe(Attestation.None);
    }

    /// <summary>The window-state no-op loads nothing and accepts saves.</summary>
    [Fact]
    public async Task NoOpWindowStateStoreLoadsNullAndAcceptsSaves()
    {
        var ct = TestContext.Current.CancellationToken;
        NoOpWindowStateStore store = new();

        (await store.LoadAsync(ct)).ShouldBeNull();
        await store.SaveAsync(new WindowState { Width = 1280, Height = 800, IsMaximized = false }, ct);
    }

    /// <summary>The single-instance no-op always acquires.</summary>
    [Fact]
    public async Task NoOpSingleInstanceGuardAlwaysAcquires()
    {
        var ct = TestContext.Current.CancellationToken;
        NoOpSingleInstanceGuard guard = new();

        (await guard.TryAcquireAsync(ct)).ShouldBeTrue();
        await guard.ReleaseAsync(ct);
    }

    /// <summary>The recovery no-op lists nothing and accepts discards.</summary>
    [Fact]
    public async Task NoOpSessionRecoveryListsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        NoOpSessionRecovery recovery = new();

        (await recovery.ListRecoverableSessionIdsAsync(ct)).ShouldBeEmpty();
        await recovery.DiscardAsync("sess-1", ct);
    }

    private static InMemoryScript Scripted(IEnumerable<HarnessEvent> events) => new()
    {
        Steps = events.Select(static evt => (InMemoryStep)new EmitStep(evt)).ToArray(),
    };

    private static HttpClient ClientFor(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.local/") };

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var collected = new List<HarnessEvent>();
        await foreach (var evt in events)
        {
            collected.Add(evt);
        }

        return collected;
    }

    /// <summary>
    /// Scripted API origin: creates session <c>sess-1</c>, accepts prompts, then serves SSE.
    /// The first event stream delivers Seqs 1..5 and ends (connection drop); a stream carrying
    /// <c>Last-Event-ID</c> replays Seqs 5..12 with Seq 12 terminal, so Seq 5 overlaps on purpose.
    /// </summary>
    private sealed class ScriptedApiHandler : HttpMessageHandler
    {
        /// <summary>Gets every Last-Event-ID value received, in arrival order.</summary>
        public List<string?> ResumePositions { get; } = [];

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(201, """{"id":"sess-1","ownerId":"local-dev"}"""));
            }

            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions/sess-1/prompts", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(202, """{"sessionId":"sess-1","status":"accepted"}"""));
            }

            if (request.Method == HttpMethod.Get && string.Equals(path, "/sessions/sess-1/events", StringComparison.Ordinal))
            {
                if (request.Headers.TryGetValues(RemoteEngineHost.LastEventIdHeader, out var values))
                {
                    var position = values.FirstOrDefault();
                    ResumePositions.Add(position);
                    return Task.FromResult(Sse(Enumerable.Range(5, 7).Select(Frame).Append(Terminal(12))));
                }

                return Task.FromResult(Sse(Enumerable.Range(1, 5).Select(Frame)));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static string Frame(int seq) =>
            $"id: {seq}\nevent: text_delta\ndata: {{\"kind\":\"text_delta\",\"delta\":\"t{seq}\"}}\n\n";

        private static string Terminal(int seq) =>
            $"id: {seq}\nevent: completed\ndata: {{\"kind\":\"completed\",\"reason\":\"end_turn\"}}\n\n";

        private static HttpResponseMessage Json(int status, string body)
        {
            var response = new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            return response;
        }

        private static HttpResponseMessage Sse(IEnumerable<string> frames)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Concat(frames), Encoding.UTF8, "text/event-stream"),
            };
            return response;
        }
    }

    /// <summary>
    /// Faithful SSE origin for two turns: <c>a</c> then a completion (Seq 2), and <c>b</c> then a completion (Seq 4).
    /// As the API does, a stream carries only Seqs after <c>Last-Event-ID</c> and closes after its completion.
    /// </summary>
    private sealed class TurnServerHandler : HttpMessageHandler
    {
        private static readonly (long Seq, string Data, bool Terminal)[] Events =
        [
            (1, """{"kind":"text_delta","delta":"a"}""", false),
            (2, """{"kind":"completed","reason":"end_turn"}""", true),
            (3, """{"kind":"text_delta","delta":"b"}""", false),
            (4, """{"kind":"completed","reason":"end_turn"}""", true),
        ];

        /// <summary>Gets every Last-Event-ID value received, in arrival order.</summary>
        public List<string> ResumePositions { get; } = [];

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"id":"sess-1","ownerId":"local-dev"}""", Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions/sess-1/prompts", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("""{"sessionId":"sess-1","status":"accepted"}""", Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Get && string.Equals(path, "/sessions/sess-1/events", StringComparison.Ordinal))
            {
                var after = 0L;
                if (request.Headers.TryGetValues(RemoteEngineHost.LastEventIdHeader, out var values))
                {
                    var position = values.Single();
                    ResumePositions.Add(position);
                    after = long.Parse(position, System.Globalization.CultureInfo.InvariantCulture);
                }

                var frames = new List<string>();
                foreach (var (seq, data, terminal) in Events.Where(evt => evt.Seq > after))
                {
                    frames.Add($"id: {seq}\ndata: {data}\n\n");
                    if (terminal)
                    {
                        break;
                    }
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Concat(frames), Encoding.UTF8, "text/event-stream"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    /// <summary>One scripted turn: creates the session, accepts the prompt, and serves the given SSE frames in order.</summary>
    private sealed class SingleTurnHandler(params string[] frames) : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"id":"sess-1","ownerId":"local-dev"}""", Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions/sess-1/prompts", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("""{"sessionId":"sess-1","status":"accepted"}""", Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Get && string.Equals(path, "/sessions/sess-1/events", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Concat(frames), Encoding.UTF8, "text/event-stream"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    /// <summary>An in-process engine that records how many times it was disposed.</summary>
    private sealed class FakeOmpSession : IOmpSession
    {
        /// <summary>Gets how many times the engine was disposed.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public HarnessState State { get; set; } = HarnessState.Ready;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>();

        /// <inheritdoc />
        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
            string prompt,
            PromptOptions options,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new CompletedEvent("end_turn");
        }

        /// <inheritdoc />
        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            State = HarnessState.Disposed;
            return ValueTask.CompletedTask;
        }
    }
}
