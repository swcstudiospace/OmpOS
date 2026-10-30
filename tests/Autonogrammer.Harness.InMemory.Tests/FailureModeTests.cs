using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.InMemory.Tests;

/// <summary>The six scripted failure modes of the in-memory adapter (HARN-03).</summary>
public sealed class FailureModeTests
{
    /// <summary>A process-start failure surfaces from CreateAsync and faults the session.</summary>
    [Fact]
    public async Task ProcessStartFailureSurfacesAsProcessStartHarnessException()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new InMemoryOmpAdapter(new InMemoryScript
        {
            Steps = [],
            ProcessStartFailure = new ProcessStartHarnessException("spawn failed", "omp acp"),
        }).CreateSession();

        try
        {
            var error = await Should.ThrowAsync<ProcessStartHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);

            error.Context.ShouldBe("omp acp");
            session.State.ShouldBe(HarnessState.Faulted);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A handshake timeout surfaces from CreateAsync and faults the session.</summary>
    [Fact]
    public async Task HandshakeTimeoutSurfacesAsHandshakeTimeoutHarnessException()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new InMemoryOmpAdapter(new InMemoryScript
        {
            Steps = [],
            HandshakeFailure = new HandshakeTimeoutHarnessException("no initialize response within 5s"),
        }).CreateSession();

        try
        {
            await Should.ThrowAsync<HandshakeTimeoutHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);

            session.State.ShouldBe(HarnessState.Faulted);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A malformed frame ends the stream after the preceding events and faults the session.</summary>
    [Fact]
    public async Task MalformedFrameStepSurfacesAfterPartialEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = await StartAsync(
        [
            new EmitStep(new TextDeltaEvent("partial")),
            new FailStep(new MalformedFrameHarnessException("bad frame at offset 12")),
        ], ct).ConfigureAwait(true);

        try
        {
            var seen = new List<HarnessEvent>();
            await Should.ThrowAsync<MalformedFrameHarnessException>(() => ScriptFixtures.ConsumeAsync(session, seen, ct)).ConfigureAwait(true);

            seen.ShouldBe([new TextDeltaEvent("partial")]);
            session.State.ShouldBe(HarnessState.Faulted);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A mid-stream disconnect is delivered only after the consumer has seen the partial events.</summary>
    [Fact]
    public async Task MidStreamDisconnectSurfacesAfterPartialEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = await StartAsync(
        [
            new EmitStep(new TextDeltaEvent("first")),
            new EmitStep(new TextDeltaEvent("second")),
            new FailStep(new DisconnectedHarnessException("socket reset by peer")),
        ], ct).ConfigureAwait(true);

        try
        {
            var seen = new List<HarnessEvent>();
            await Should.ThrowAsync<DisconnectedHarnessException>(() => ScriptFixtures.ConsumeAsync(session, seen, ct)).ConfigureAwait(true);

            seen.ShouldBe([new TextDeltaEvent("first"), new TextDeltaEvent("second")]);
            session.State.ShouldBe(HarnessState.Faulted);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// A bounded buffer of capacity 4 with a burst of six events overflows: the consumer receives the
    /// four buffered events, then BackpressureHarnessException.
    /// </summary>
    [Fact]
    public async Task BackpressureOverflowSurfacesAfterBufferedEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        var burst = Enumerable.Range(1, 6).Select(i => (InMemoryStep)new EmitStep(new TextDeltaEvent($"t{i}"))).ToArray();
        var session = new InMemoryOmpAdapter(new InMemoryScript { Steps = burst, ChannelCapacity = 4 }).CreateSession();
        await session.CreateAsync(ct).ConfigureAwait(true);

        try
        {
            var seen = new List<HarnessEvent>();
            await Should.ThrowAsync<BackpressureHarnessException>(() => ScriptFixtures.ConsumeAsync(session, seen, ct)).ConfigureAwait(true);

            seen.ShouldBe([new TextDeltaEvent("t1"), new TextDeltaEvent("t2"), new TextDeltaEvent("t3"), new TextDeltaEvent("t4")]);
            session.State.ShouldBe(HarnessState.Faulted);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A second CompletedEvent in the script surfaces as DuplicateCompletionHarnessException after the first.</summary>
    [Fact]
    public async Task DuplicateCompletionSurfacesAfterFirstCompletion()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = await StartAsync(
        [
            new EmitStep(new TextDeltaEvent("done")),
            new EmitStep(new CompletedEvent("end_turn")),
            new EmitStep(new CompletedEvent("end_turn")),
        ], ct).ConfigureAwait(true);

        try
        {
            var seen = new List<HarnessEvent>();
            await Should.ThrowAsync<DuplicateCompletionHarnessException>(() => ScriptFixtures.ConsumeAsync(session, seen, ct)).ConfigureAwait(true);

            seen.ShouldBe([new TextDeltaEvent("done"), new CompletedEvent("end_turn")]);
            session.State.ShouldBe(HarnessState.Faulted);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A stream that ends without a CompletedEvent is reported as a disconnect and faults the session.</summary>
    [Fact]
    public async Task StreamEndingWithoutCompletionSurfacesAsDisconnected()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = await StartAsync([new EmitStep(new TextDeltaEvent("cut short"))], ct).ConfigureAwait(true);

        try
        {
            var seen = new List<HarnessEvent>();
            await Should.ThrowAsync<DisconnectedHarnessException>(() => ScriptFixtures.ConsumeAsync(session, seen, ct)).ConfigureAwait(true);

            seen.ShouldBe([new TextDeltaEvent("cut short")]);
            session.State.ShouldBe(HarnessState.Faulted);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static async Task<IOmpSession> StartAsync(InMemoryStep[] steps, CancellationToken ct)
    {
        var session = new InMemoryOmpAdapter(new InMemoryScript { Steps = steps }).CreateSession();
        await session.CreateAsync(ct).ConfigureAwait(false);
        return session;
    }
}
