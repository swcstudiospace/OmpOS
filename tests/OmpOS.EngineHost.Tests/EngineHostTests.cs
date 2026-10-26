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

        var seen = await CollectDroppedAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct));
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

        _ = await CollectDroppedAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct));
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

    /// <summary>
    /// H1/H2: with a held completion present, the next prompt's stream yields none of the held events, the POST
    /// is still sent, and <c>TakeHeldEvents</c> returns the held events in Seq order. Both streams resume from
    /// the delivered cursor, proving a hold never moves it.
    /// </summary>
    [Fact]
    public async Task HeldCompletionIsNotYieldedByNextPrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedApiHandler();
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        _ = await CollectDroppedAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct)).ConfigureAwait(true);
        await host.ReconnectAsync(ct).ConfigureAwait(true);

        var seen = await CollectAsync(host.SubmitPromptAsync("next prompt", new PromptOptions(), ct)).ConfigureAwait(true);

        handler.PromptPosts.ShouldBe(2);
        seen.ShouldBeEmpty();
        handler.ResumePositions.ShouldBe(["5", "5"]);

        var held = host.TakeHeldEvents();
        held.OfType<TextDeltaEvent>().Select(static evt => evt.Text).ShouldBe(["t6", "t7", "t8", "t9", "t10", "t11"]);
        held[^1].ShouldBeOfType<CompletedEvent>().Reason.ShouldBe("end_turn");
        held.Count.ShouldBe(7);
        host.TakeHeldEvents().ShouldBeEmpty();
    }

    /// <summary>
    /// H2/H3: a resume that lands while a turn is still streaming opens no second fetch: the live stream owns
    /// the session and delivers every Seq exactly once, in increasing order, while the resume holds nothing.
    /// The gate makes this deterministic: the terminal frame stays blocked until the resume has returned.
    /// </summary>
    [Fact]
    public async Task ResumeDuringLiveTurnDeliversEachEventExactlyOnceInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new GatedTurnHandler();
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);
        var raised = 0;
        host.MissedEventsHeld += (_, _) => raised++;

        await using var turn = host.SubmitPromptAsync("one", new PromptOptions(), ct).WithCancellation(ct).GetAsyncEnumerator();
        (await turn.MoveNextAsync()).ShouldBeTrue();
        turn.Current.ShouldBeOfType<TextDeltaEvent>().Text.ShouldBe("a");

        // The live stream is provably open: it delivered an event but its terminal frame is still gated.
        await host.ReconnectAsync(ct).ConfigureAwait(true);

        raised.ShouldBe(0);
        handler.Gets.ShouldBe(1);

        handler.Release();
        (await turn.MoveNextAsync()).ShouldBeTrue();
        turn.Current.ShouldBeOfType<CompletedEvent>().Reason.ShouldBe("end_turn");
        (await turn.MoveNextAsync()).ShouldBeFalse();

        host.TakeHeldEvents().ShouldBeEmpty();
    }

    /// <summary>
    /// Lease: a hold fetch gated mid-stream owns the session stream, so a live turn started while it is gated
    /// issues no fetch and yields nothing until the hold completes. The held events come out in order through
    /// <c>TakeHeldEvents</c>, then the live reply follows in order, each Seq exactly once. The poll only waits
    /// for the gate-driven fetch to arrive; every assertion is on sequence numbers and string contents.
    /// </summary>
    [Fact]
    public async Task LiveTurnWaitsForGatedHoldThenStreamsAfterItInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new HoldGatedHandler();
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);
        var raised = 0;
        host.MissedEventsHeld += (_, _) => raised++;

        _ = await CollectDroppedAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct)).ConfigureAwait(true);

        var hold = host.ReconnectAsync(ct);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (handler.LabelledGets == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token).ConfigureAwait(true);
        }

        handler.LabelledGets.ShouldBe(1);
        var live = host.SubmitPromptAsync("next", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        try
        {
            var liveNext = live.MoveNextAsync();
            liveNext.IsCompleted.ShouldBeFalse();
            handler.PromptPosts.ShouldBe(1);

            handler.Release();
            await hold.ConfigureAwait(true);
            raised.ShouldBe(1);

            var held = host.TakeHeldEvents();
            held.OfType<TextDeltaEvent>().Select(static evt => evt.Text).ShouldBe(["t6", "t7", "t8", "t9", "t10", "t11"]);
            held[^1].ShouldBeOfType<CompletedEvent>().Reason.ShouldBe("end_turn");
            held.Count.ShouldBe(7);

            (await liveNext.ConfigureAwait(true)).ShouldBeTrue();
            live.Current.ShouldBeOfType<TextDeltaEvent>().Text.ShouldBe("live13");
            (await live.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            live.Current.ShouldBeOfType<CompletedEvent>().Reason.ShouldBe("end_turn");
            (await live.MoveNextAsync().ConfigureAwait(true)).ShouldBeFalse();

            handler.PromptPosts.ShouldBe(2);
            host.TakeHeldEvents().ShouldBeEmpty();
        }
        finally
        {
            await live.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A faulted engine that the host replaces is disposed; the replacement serves the next prompt.</summary>
    [Fact]
    public async Task FaultedEngineIsDisposedWhenReplaced()
    {
        var ct = TestContext.Current.CancellationToken;
        var engines = new List<FakeOmpSession>();
        var host = new InProcessEngineHost(
            () =>
            {
                var engine = new FakeOmpSession();
                engines.Add(engine);
                return engine;
            },
            new EngineHostDescriptor { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None });
        await using var hostScope = host.ConfigureAwait(true);

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

    /// <summary>Cancelling a host with no active prompt does nothing: it sends no request, and the next prompt still runs normally.</summary>
    [Fact]
    public async Task CancelWithoutPromptCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        HarnessEvent[] script = [new TextDeltaEvent("Hello"), new CompletedEvent("end_turn")];
        await using var inProcess = InProcessEngineHost.ForInMemory(new InMemoryOmpAdapter(Scripted(script)));
        using var handler = new ScriptedApiHandler();
        using var http = ClientFor(handler);
        var remote = new RemoteEngineHost(http);

        await Should.NotThrowAsync(() => inProcess.CancelAsync(ct));
        await Should.NotThrowAsync(() => remote.CancelAsync(ct));

        // The remote host reached no origin for the cancel, so no prompt was posted and no stream was resumed.
        handler.PromptPosts.ShouldBe(0);
        handler.ResumePositions.ShouldBeEmpty();

        // A cancel with nothing to cancel must not poison the host: the next prompt streams its whole script.
        (await CollectAsync(inProcess.SubmitPromptAsync("prompt", new PromptOptions(), ct))).ShouldBe(script);
        (await CollectDroppedAsync(remote.SubmitPromptAsync("prompt", new PromptOptions(), ct))).Count.ShouldBe(5);
        handler.PromptPosts.ShouldBe(1);
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
        await foreach (var evt in events.ConfigureAwait(true))
        {
            collected.Add(evt);
        }

        return collected;
    }

    /// <summary>Collects a prompt whose stream drops before a terminal event: the events before the drop, then the surfaced error.</summary>
    private static async Task<List<HarnessEvent>> CollectDroppedAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var collected = new List<HarnessEvent>();
        await Should.ThrowAsync<DisconnectedHarnessException>(async () =>
        {
            await foreach (var evt in events.ConfigureAwait(true))
            {
                collected.Add(evt);
            }
        }).ConfigureAwait(true);

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

        /// <summary>Gets how many prompts were submitted, proving a new message still sends after an undrained resume.</summary>
        public int PromptPosts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(201, """{"id":"sess-1","ownerId":"local-dev"}"""));
            }

            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions/sess-1/prompts", StringComparison.Ordinal))
            {
                PromptPosts++;
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

    /// <summary>Streams <paramref name="first"/> at once, then blocks until the gate before <paramref name="rest"/>.</summary>
    /// <param name="first">The frame bytes served immediately.</param>
    /// <param name="rest">The frame bytes served after the gate completes.</param>
    /// <param name="gate">Completes when the remainder may be served.</param>
    private sealed class GatedSseContent(string first, string rest, Task gate) : HttpContent
    {
        /// <inheritdoc />
        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new GateStream(Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(rest), gate));

        /// <inheritdoc />
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new NotSupportedException("GatedSseContent streams through CreateContentReadStreamAsync.");

        /// <inheritdoc />
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            throw new NotSupportedException("GatedSseContent streams through CreateContentReadStreamAsync.");

        /// <inheritdoc />
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>Serves two byte ranges with a gate between them, then ends the stream.</summary>
    /// <param name="first">The bytes served immediately.</param>
    /// <param name="rest">The bytes served after the gate completes.</param>
    /// <param name="gate">Completes when <paramref name="rest"/> may be served.</param>
    private sealed class GateStream(byte[] first, byte[] rest, Task gate) : Stream
    {
        private int _stage;
        private int _offset;

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_stage == 1)
            {
#pragma warning disable VSTHRD003 // The gate is test-owned and completes via RunContinuationsAsynchronously.
                await gate.ConfigureAwait(false);
#pragma warning restore VSTHRD003
                cancellationToken.ThrowIfCancellationRequested();
            }
            else if (_stage > 1)
            {
                return 0;
            }

            var current = _stage == 0 ? first : rest;
            var count = Math.Min(buffer.Length, current.Length - _offset);
            current.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            if (_offset >= current.Length)
            {
                _stage++;
                _offset = 0;
            }

            return count;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("GateStream serves reads asynchronously only.");

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// A turn whose event stream stays open: the first GET streams one text frame, then blocks until
    /// <see cref="Release"/> before serving the terminal frame. A second GET serves a terminal frame at
    /// once; it is only reachable when a resume wrongly opens its own fetch during the live turn.
    /// </summary>
    private sealed class GatedTurnHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _gets;

        /// <summary>Gets how many event-stream fetches reached the origin.</summary>
        public int Gets => _gets;

        /// <summary>Serves the terminal frame to the live stream. Idempotent.</summary>
        public void Release() => _gate.TrySetResult();

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions", StringComparison.Ordinal))
            {
                return Task.FromResult(Created());
            }

            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions/sess-1/prompts", StringComparison.Ordinal))
            {
                return Task.FromResult(Accepted());
            }

            if (request.Method == HttpMethod.Get && string.Equals(path, "/sessions/sess-1/events", StringComparison.Ordinal))
            {
                _gets++;
                return Task.FromResult(_gets == 1 ? Gated() : Immediate());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Created() => new(HttpStatusCode.Created)
        {
            Content = new StringContent("""{"id":"sess-1","ownerId":"local-dev"}""", Encoding.UTF8, "application/json"),
        };

        private static HttpResponseMessage Accepted() => new(HttpStatusCode.Accepted)
        {
            Content = new StringContent("""{"sessionId":"sess-1","status":"accepted"}""", Encoding.UTF8, "application/json"),
        };

        private HttpResponseMessage Gated() => new(HttpStatusCode.OK)
        {
            Content = new GatedSseContent(
                "id: 1\nevent: text_delta\ndata: {\"kind\":\"text_delta\",\"delta\":\"a\"}\n\n",
                "id: 2\nevent: completed\ndata: {\"kind\":\"completed\",\"reason\":\"end_turn\"}\n\n",
                _gate.Task),
        };

        private static HttpResponseMessage Immediate() => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "id: 2\nevent: completed\ndata: {\"kind\":\"completed\",\"reason\":\"end_turn\"}\n\n",
                Encoding.UTF8,
                "text/event-stream"),
        };
    }

    /// <summary>
    /// A hold fetch gated mid-stream: the first event-stream request without <c>Last-Event-ID</c> serves turn 1 and
    /// ends. The first request carrying <c>Last-Event-ID</c> serves one replayed frame, one held frame, then blocks
    /// until <see cref="Release"/> before the rest of the held turn. Any later request serves only the next turn's
    /// reply; it is reachable only when a live turn wrongly fetches alongside the hold.
    /// </summary>
    private sealed class HoldGatedHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _labelledGets;

        /// <summary>Gets how many fetches carried <c>Last-Event-ID</c>, proving whether the live turn fetched early.</summary>
        public int LabelledGets => _labelledGets;

        /// <summary>Gets how many prompts were submitted.</summary>
        public int PromptPosts { get; private set; }

        /// <summary>Serves the rest of the held turn. Idempotent.</summary>
        public void Release() => _gate.TrySetResult();

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions", StringComparison.Ordinal))
            {
                return Task.FromResult(Created());
            }

            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions/sess-1/prompts", StringComparison.Ordinal))
            {
                PromptPosts++;
                return Task.FromResult(Accepted());
            }

            if (request.Method == HttpMethod.Get && string.Equals(path, "/sessions/sess-1/events", StringComparison.Ordinal))
            {
                if (!request.Headers.TryGetValues(RemoteEngineHost.LastEventIdHeader, out _))
                {
                    return Task.FromResult(Immediate(1, 5));
                }

                _labelledGets++;
                return Task.FromResult(_labelledGets == 1 ? GatedHold() : LiveReply());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Created() => new(HttpStatusCode.Created)
        {
            Content = new StringContent("""{"id":"sess-1","ownerId":"local-dev"}""", Encoding.UTF8, "application/json"),
        };

        private static HttpResponseMessage Accepted() => new(HttpStatusCode.Accepted)
        {
            Content = new StringContent("""{"sessionId":"sess-1","status":"accepted"}""", Encoding.UTF8, "application/json"),
        };

        private HttpResponseMessage GatedHold() => new(HttpStatusCode.OK)
        {
            Content = new GatedSseContent(
                Frame(5) + Frame(6),
                Frames(7, 11) + Terminal(12),
                _gate.Task),
        };

        private static HttpResponseMessage LiveReply() => new(HttpStatusCode.OK)
        {
            Content = new StringContent(Frame(13, "live") + Terminal(14), Encoding.UTF8, "text/event-stream"),
        };

        private static HttpResponseMessage Immediate(int first, int last) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(Frames(first, last), Encoding.UTF8, "text/event-stream"),
        };

        private static string Frames(int first, int last, string text = "t") =>
            string.Concat(Enumerable.Range(first, last - first + 1).Select(seq => Frame(seq, text)));

        private static string Frame(int seq, string text = "t") =>
            $"id: {seq}\nevent: text_delta\ndata: {{\"kind\":\"text_delta\",\"delta\":\"{text}{seq}\"}}\n\n";

        private static string Terminal(int seq) =>
            $"id: {seq}\nevent: completed\ndata: {{\"kind\":\"completed\",\"reason\":\"end_turn\"}}\n\n";
    }

    /// <summary>An in-process engine that records how many times it was disposed.</summary>
    private sealed class FakeOmpSession : IOmpSession
    {
        /// <summary>Gets how many times the engine was disposed.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public HarnessState State { get; set; } = HarnessState.Ready;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

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
