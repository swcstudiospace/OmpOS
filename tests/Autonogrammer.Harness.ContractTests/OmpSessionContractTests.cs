using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ContractTests;

/// <summary>
/// Behavioral contract every <see cref="IOmpSession"/> must satisfy. Derive a concrete class that
/// supplies a fresh, not-yet-created session whose prompt stream yields <see cref="ScriptedSequence"/>.
/// </summary>
/// <typeparam name="T">The session type under test.</typeparam>
public abstract class OmpSessionContractTests<T> where T : IOmpSession
{
    /// <summary>Creates a new, not-yet-created session whose prompt stream yields <see cref="ScriptedSequence"/>.</summary>
    /// <returns>A session in <see cref="HarnessState.Disconnected"/>.</returns>
    protected abstract T CreateSession();

    /// <summary>
    /// Gets the events a happy-path prompt must yield, in order. Must contain at least two events
    /// and end with a <see cref="CompletedEvent"/>.
    /// </summary>
    protected abstract IReadOnlyList<HarnessEvent> ScriptedSequence { get; }

    /// <summary>A new session is disconnected; creating it reaches Ready.</summary>
    [Fact]
    public async Task CreateTransitionsToReady()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = CreateSession();
        try
        {
            session.State.ShouldBe(HarnessState.Disconnected);

            await session.CreateAsync(ct).ConfigureAwait(true);

            session.State.ShouldBe(HarnessState.Ready);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A happy-path prompt yields the scripted sequence exactly, in order, and returns to Ready.</summary>
    [Fact]
    public async Task SubmitHappyPathYieldsScriptedSequenceInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        ScriptedSequence.Count.ShouldBeGreaterThan(1);
        ScriptedSequence[^1].ShouldBeOfType<CompletedEvent>();

        var session = CreateSession();
        try
        {
            await session.CreateAsync(ct).ConfigureAwait(true);

            var received = new List<HarnessEvent>();
            await AsyncStreamHelpers.DrainAsync(session.SubmitPromptAsync("hello", new PromptOptions(), ct), received, ct).ConfigureAwait(true);

            received.ShouldBe(ScriptedSequence);
            session.State.ShouldBe(HarnessState.Ready);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Cancelling mid-stream stops the scripted events, yields CompletedEvent("cancelled") last, and returns to Ready.</summary>
    [Fact]
    public async Task CancelDuringStreamYieldsCancelledCompletionAndReturnsToReady()
    {
        var ct = TestContext.Current.CancellationToken;
        ScriptedSequence.Count.ShouldBeGreaterThan(1);

        var session = CreateSession();
        try
        {
            await session.CreateAsync(ct).ConfigureAwait(true);

            var enumerator = session.SubmitPromptAsync("hello", new PromptOptions(), ct).GetAsyncEnumerator(ct);
            try
            {
                (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
                enumerator.Current.ShouldBe(ScriptedSequence[0]);

                await session.CancelAsync(ct).ConfigureAwait(true);

                var rest = new List<HarnessEvent>();
                while (await enumerator.MoveNextAsync().ConfigureAwait(true))
                {
                    rest.Add(enumerator.Current);
                }

                rest.ShouldBe(new HarnessEvent[] { new CompletedEvent("cancelled") });
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(true);
            }

            session.State.ShouldBe(HarnessState.Ready);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Disposing twice is safe and leaves the session in Disposed.</summary>
    [Fact]
    public async Task DisposeIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = CreateSession();
        await session.CreateAsync(ct).ConfigureAwait(true);

        await session.DisposeAsync().ConfigureAwait(true);
        await session.DisposeAsync().ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Disposed);
    }

    /// <summary>Submitting after dispose throws ObjectDisposedException.</summary>
    [Fact]
    public async Task SubmitAfterDisposeThrowsObjectDisposedException()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = CreateSession();
        await session.CreateAsync(ct).ConfigureAwait(true);
        await session.DisposeAsync().ConfigureAwait(true);

        await Should.ThrowAsync<ObjectDisposedException>(() =>
            AsyncStreamHelpers.DrainAsync(session.SubmitPromptAsync("late", new PromptOptions(), ct), new List<HarnessEvent>(), ct)).ConfigureAwait(true);
    }
}
