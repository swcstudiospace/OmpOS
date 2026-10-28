using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.InMemory.Tests;

/// <summary>Terminal-event handling of the in-memory session: an error ends a turn cleanly, a silent close does not.</summary>
public sealed class TerminalEventTests
{
    /// <summary>An ErrorEvent ends the turn without a disconnect: the error arrives last and the session returns to Ready.</summary>
    [Fact]
    public async Task ErrorEventEndsTurnWithoutDisconnect()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = await StartAsync(
        [
            new EmitStep(new TextDeltaEvent("partial")),
            new EmitStep(new ErrorEvent("boom", Retryable: true)),
        ], ct).ConfigureAwait(true);

        try
        {
            var seen = new List<HarnessEvent>();
            await ScriptFixtures.ConsumeAsync(session, seen, ct).ConfigureAwait(true);

            seen.ShouldBe([new TextDeltaEvent("partial"), new ErrorEvent("boom", Retryable: true)]);
            session.State.ShouldBe(HarnessState.Ready);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Nothing reaches the consumer after an ErrorEvent: the error ends the stream.</summary>
    [Fact]
    public async Task EventsAfterErrorEventAreNotDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = await StartAsync(
        [
            new EmitStep(new ErrorEvent("boom", Retryable: false)),
            new EmitStep(new TextDeltaEvent("late")),
        ], ct).ConfigureAwait(true);

        try
        {
            var seen = new List<HarnessEvent>();
            await ScriptFixtures.ConsumeAsync(session, seen, ct).ConfigureAwait(true);

            seen.ShouldBe([new ErrorEvent("boom", Retryable: false)]);
            session.State.ShouldBe(HarnessState.Ready);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A diagnostic is not terminal: a stream that closes after one still surfaces as a disconnect.</summary>
    [Fact]
    public async Task DiagnosticEventDoesNotCountAsTerminal()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = await StartAsync(
        [
            new EmitStep(new TextDeltaEvent("cut")),
            new EmitStep(new DiagnosticEvent("slow engine")),
        ], ct).ConfigureAwait(true);

        try
        {
            var seen = new List<HarnessEvent>();
            await Should.ThrowAsync<DisconnectedHarnessException>(() => ScriptFixtures.ConsumeAsync(session, seen, ct)).ConfigureAwait(true);

            seen.ShouldBe([new TextDeltaEvent("cut"), new DiagnosticEvent("slow engine")]);
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
