using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OmpOS.Contracts.Environments;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Contracts.Sessions;
using OmpOS.EngineHost;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// Reconnecting a remote session: the interrupted turn, the retry schedule on a manual clock, and <c>session/load</c> with the stored id. Every
/// session is scripted and every wait runs on <see cref="ManualTimeProvider"/>, so no test spawns a process or sleeps.
/// </summary>
public sealed class ReconnectingEngineHostTests
{
    private static readonly EngineHostDescriptor Descriptor = new() { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None };

    private static DisconnectedHarnessException Unreachable() => new DisconnectedHarnessException("ssh exited with code 255. The host cannot be reached.");

    private static (ReconnectingEngineHost Host, List<ScriptedSession> Sessions, ManualTimeProvider Clock) Create(params SessionScript[] scripts)
    {
        var sessions = new List<ScriptedSession>();
        var clock = new ManualTimeProvider();
        return (new ReconnectingEngineHost(NewInner(sessions, scripts), clock: clock), sessions, clock);
    }

    private static InProcessEngineHost NewInner(List<ScriptedSession> sessions, SessionScript[] scripts) => new(
        () =>
        {
            var script = sessions.Count < scripts.Length ? scripts[sessions.Count] : new SessionScript();
            var session = new ScriptedSession(script);
            sessions.Add(session);
            return session;
        },
        Descriptor,
        resumeSessionId: null,
        resumeAfterFault: true);

    private static async Task<List<HarnessEvent>> PromptAsync(ReconnectingEngineHost host, ManualTimeProvider clock, CancellationToken cancellationToken)
    {
        var events = new List<HarnessEvent>();
        var pump = DrainAsync(host, events, cancellationToken);
        while (!pump.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!clock.AdvanceToNextTimer())
            {
                await Task.Yield();
            }
        }

        await pump.ConfigureAwait(true);
        return events;
    }

    private static async Task DrainAsync(ReconnectingEngineHost host, List<HarnessEvent> events, CancellationToken cancellationToken)
    {
        await foreach (var evt in host.SubmitPromptAsync("prompt", new PromptOptions(), cancellationToken).ConfigureAwait(false))
        {
            events.Add(evt);
        }
    }

    /// <summary>A drop while a turn runs: the turn is interrupted (retryable error, never a completion), the session is loaded again with the stored id after one second, and the next prompt works.</summary>
    [Fact(Timeout = 30000)]
    public async Task ADropMidTurnInterruptsTheTurnAndReloadsTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, sessions, clock) = Create(new SessionScript { Events = [new TextDeltaEvent("partial")], DropAfterEvents = true });
        await using var hostScope = host.ConfigureAwait(true);
        var states = new ConcurrentQueue<RemoteConnectionState>();
        var interrupted = new ConcurrentQueue<TurnInterruptedEventArgs>();
        host.ConnectionStateChanged += (_, args) => states.Enqueue(args.State);
        host.TurnInterrupted += (_, args) => interrupted.Enqueue(args);
        await host.ConnectAsync(ct).ConfigureAwait(true);

        var events = await PromptAsync(host, clock, ct).ConfigureAwait(true);

        events.Count.ShouldBe(3);
        events[0].ShouldBe(new TextDeltaEvent("partial"));
        events[1].ShouldBeOfType<DiagnosticEvent>();
        var error = events[2].ShouldBeOfType<ErrorEvent>();
        error.Retryable.ShouldBeTrue();
        error.Message.ShouldContain("did not finish", Case.Sensitive);
        events.OfType<CompletedEvent>().ShouldBeEmpty();

        var args = interrupted.ShouldHaveSingleItem();
        args.Status.ShouldBe(SessionStatus.Interrupted);
        args.NativeSessionId.ShouldBe("native-1");
        sessions.Count.ShouldBe(2);
        sessions[1].ResumedWith.ShouldBe("native-1");
        sessions[1].Created.ShouldBeFalse();
        sessions[0].Disposed.ShouldBeTrue();
        clock.RequestedDelays.ShouldBe([TimeSpan.FromSeconds(1)]);
        host.State.ShouldBe(RemoteConnectionState.Connected);
        states.ToArray().ShouldBe([
            RemoteConnectionState.Connecting,
            RemoteConnectionState.Connected,
            RemoteConnectionState.Reconnecting,
            RemoteConnectionState.Reconnecting,
            RemoteConnectionState.Connected,
        ]);

        var next = await PromptAsync(host, clock, ct).ConfigureAwait(true);
        next.ShouldBe([new TextDeltaEvent("hello"), new CompletedEvent("end_turn")]);
        sessions.Count.ShouldBe(2);
    }

    /// <summary>Five failed attempts wait 1, 2, 4, 8 and 16 seconds, report the host unreachable, and the next prompt starts a new round that succeeds without waiting once the host is back.</summary>
    [Fact(Timeout = 30000)]
    public async Task FiveFailedAttemptsMakeTheHostUnreachableUntilItReturns()
    {
        var ct = TestContext.Current.CancellationToken;
        var failing = new SessionScript { ResumeFailure = Unreachable() };
        var (host, sessions, clock) = Create(
            new SessionScript { DropAfterEvents = true, Events = [] },
            failing,
            failing,
            failing,
            failing,
            failing);
        await using var hostScope = host.ConfigureAwait(true);
        var attempts = new ConcurrentQueue<int>();
        host.ConnectionStateChanged += (_, args) =>
        {
            if (args.State == RemoteConnectionState.Reconnecting && args.Attempt > 0)
            {
                attempts.Enqueue(args.Attempt);
            }
        };
        await host.ConnectAsync(ct).ConfigureAwait(true);

        var events = await PromptAsync(host, clock, ct).ConfigureAwait(true);

        clock.RequestedDelays.ShouldBe([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16)]);
        attempts.ToArray().ShouldBe([1, 2, 3, 4, 5]);
        host.State.ShouldBe(RemoteConnectionState.Unreachable);
        var error = events.OfType<ErrorEvent>().Single();
        error.Message.ShouldContain("after 5 attempts", Case.Sensitive);
        error.Retryable.ShouldBeTrue();
        sessions.Count.ShouldBe(6);
        sessions.Skip(1).ShouldAllBe(session => session.ResumedWith == null && session.Disposed);

        var delaysBefore = clock.RequestedDelays.Count;
        var recovered = await PromptAsync(host, clock, ct).ConfigureAwait(true);

        recovered.ShouldBe([new TextDeltaEvent("hello"), new CompletedEvent("end_turn")]);
        clock.RequestedDelays.Count.ShouldBe(delaysBefore);
        host.State.ShouldBe(RemoteConnectionState.Connected);
        sessions[^1].ResumedWith.ShouldBe("native-1");
    }

    /// <summary>A connection that dropped while no turn ran is repaired by the next prompt through the stored session, without an error and without waiting.</summary>
    [Fact(Timeout = 30000)]
    public async Task AnIdleDropIsRepairedByTheNextPrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, sessions, clock) = Create();
        await using var hostScope = host.ConfigureAwait(true);
        await host.ConnectAsync(ct).ConfigureAwait(true);
        sessions[0].State = HarnessState.Faulted;

        var events = await PromptAsync(host, clock, ct).ConfigureAwait(true);

        events.ShouldBe([new TextDeltaEvent("hello"), new CompletedEvent("end_turn")]);
        sessions.Count.ShouldBe(2);
        sessions[1].ResumedWith.ShouldBe("native-1");
        clock.RequestedDelays.ShouldBeEmpty();
        host.Inner.NativeSessionId.ShouldBe("native-1");
    }

    /// <summary>A refusal that is not a lost connection (the engine does not know the session) is not retried: it reaches the caller.</summary>
    [Fact(Timeout = 30000)]
    public async Task ARefusedSessionIsNotRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, sessions, clock) = Create(new SessionScript(), new SessionScript { ResumeFailure = new InvalidOperationException("session not found") });
        await using var hostScope = host.ConfigureAwait(true);
        await host.ConnectAsync(ct).ConfigureAwait(true);
        sessions[0].State = HarnessState.Faulted;

        await Should.ThrowAsync<InvalidOperationException>(() => PromptAsync(host, clock, ct)).ConfigureAwait(true);

        clock.RequestedDelays.ShouldBeEmpty();
    }

    /// <summary>The first connection is not retried: a failure there is a configuration problem the user must see.</summary>
    [Fact(Timeout = 30000)]
    public async Task TheFirstConnectionIsNotRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, _, clock) = Create(new SessionScript { CreateFailure = Unreachable() });
        await using var hostScope = host.ConfigureAwait(true);

        await Should.ThrowAsync<DisconnectedHarnessException>(() => host.ConnectAsync(ct)).ConfigureAwait(true);

        host.State.ShouldBe(RemoteConnectionState.Disconnected);
        clock.RequestedDelays.ShouldBeEmpty();
    }

    /// <summary>Cancelling during a backoff wait stops the reconnect.</summary>
    [Fact(Timeout = 30000)]
    public async Task CancellingDuringTheBackoffStopsTheReconnect()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, _, clock) = Create(new SessionScript { DropAfterEvents = true, Events = [] });
        await using var hostScope = host.ConfigureAwait(true);
        await host.ConnectAsync(ct).ConfigureAwait(true);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var events = new List<HarnessEvent>();
        var pump = DrainAsync(host, events, cancel.Token);
        while (clock.PendingTimers == 0)
        {
            await Task.Yield();
        }

        await cancel.CancelAsync().ConfigureAwait(true);

        var thrown = await Record.ExceptionAsync(() => pump.WaitAsync(CancellationToken.None)).ConfigureAwait(true);
        thrown.ShouldBeAssignableTo<OperationCanceledException>();
        clock.RequestedDelays.ShouldBe([TimeSpan.FromSeconds(1)]);
    }

    /// <summary>Reconnecting without the resume option would start an empty conversation, so the host refuses such an inner host.</summary>
    [Fact]
    public async Task AnInnerHostThatDoesNotResumeIsRefused()
    {
        var inner = new InProcessEngineHost(() => new ScriptedSession(new SessionScript()), Descriptor);
        await using var innerScope = inner.ConfigureAwait(true);
        var resuming = NewInner([], []);
        await using var resumingScope = resuming.ConfigureAwait(true);

        Should.Throw<ArgumentException>(() => new ReconnectingEngineHost(inner));
        Should.Throw<ArgumentException>(() => new ReconnectingEngineHost(resuming, new ReconnectPolicy { MaxAttempts = 0 }));
    }

    private sealed record SessionScript
    {
        public string CreatedId { get; init; } = "native-1";

        public Exception? CreateFailure { get; init; }

        public Exception? ResumeFailure { get; init; }

        public IReadOnlyList<HarnessEvent> Events { get; init; } = [new TextDeltaEvent("hello")];

        public bool DropAfterEvents { get; init; }
    }

    private sealed class ScriptedSession(SessionScript script) : IOmpSession, IOmpSessionIdentity
    {
        public string? NativeSessionId { get; private set; }

        public bool Created { get; private set; }

        public string? ResumedWith { get; private set; }

        public bool Disposed { get; private set; }

        public HarnessState State { get; set; } = HarnessState.Ready;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public Task CreateAsync(CancellationToken cancellationToken)
        {
            if (script.CreateFailure is { } failure)
            {
                throw failure;
            }

            Created = true;
            NativeSessionId = script.CreatedId;
            return Task.CompletedTask;
        }

        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
        {
            if (script.ResumeFailure is { } failure)
            {
                throw failure;
            }

            ResumedWith = sessionId;
            NativeSessionId = sessionId;
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            foreach (var evt in script.Events)
            {
                yield return evt;
            }

            if (script.DropAfterEvents)
            {
                State = HarnessState.Faulted;
                throw new DisconnectedHarnessException("ssh exited with code 255.");
            }

            yield return new CompletedEvent("end_turn");
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            State = HarnessState.Disposed;
            return ValueTask.CompletedTask;
        }
    }
}
