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
    /// The remote host streams events 1..5, survives the connection drop, and replays 6..12
    /// exactly once on <c>ReconnectAsync</c> (the replay overlaps Seq 5 to prove de-duplication).
    /// </summary>
    [Fact]
    public async Task RemoteResumeReplaysMissedEventsExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedApiHandler();
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        var seen = new List<HarnessEvent>();
        await foreach (var evt in host.SubmitPromptAsync("prompt", new PromptOptions(), ct))
        {
            seen.Add(evt);
        }

        seen.Count.ShouldBe(5);
        await foreach (var evt in host.ReconnectAsync(ct))
        {
            seen.Add(evt);
        }

        var texts = seen.OfType<TextDeltaEvent>().Select(static evt => evt.Text).ToArray();
        texts.ShouldBe(["t1", "t2", "t3", "t4", "t5", "t6", "t7", "t8", "t9", "t10", "t11"]);
        seen.OfType<CompletedEvent>().Select(static evt => evt.Reason).ShouldBe(["end_turn"]);
        seen.Count.ShouldBe(12);
        handler.ResumePositions.ShouldBe(["5"]);
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

    private static HttpClient ClientFor(ScriptedApiHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.local/") };

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
}
