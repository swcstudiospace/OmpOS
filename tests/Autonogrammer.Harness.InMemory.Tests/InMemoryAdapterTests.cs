using Autonogrammer.Contracts.Ui;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.InMemory.Tests;

/// <summary>Behavior of the scripted in-memory adapter on the happy path and under concurrency.</summary>
public sealed class InMemoryAdapterTests
{
    /// <summary>The scripted happy path yields events in order, including the UiBlock, then disposes cleanly.</summary>
    [Fact]
    public async Task HappyPathYieldsScriptedEventOrderIncludingUiBlock()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new InMemoryOmpAdapter(ScriptFixtures.Emit(ScriptFixtures.HappySequence)).CreateSession();
        session.State.ShouldBe(HarnessState.Disconnected);

        await session.CreateAsync(ct).ConfigureAwait(true);
        session.State.ShouldBe(HarnessState.Ready);

        var seen = new List<HarnessEvent>();
        await ScriptFixtures.ConsumeAsync(session, seen, ct).ConfigureAwait(true);

        seen.ShouldBe(ScriptFixtures.HappySequence);
        seen.OfType<UiBlockEvent>().Single().Block.ShouldBeOfType<MarkdownBlock>();
        session.State.ShouldBe(HarnessState.Ready);

        await session.DisposeAsync().ConfigureAwait(true);
        session.State.ShouldBe(HarnessState.Disposed);
    }

    /// <summary>The in-memory engine advertises no capabilities.</summary>
    [Fact]
    public void AdvertisedCapabilitiesAreEmpty()
    {
        var session = new InMemoryOmpAdapter(ScriptFixtures.Emit([])).CreateSession();

        session.AdvertisedCapabilities.ShouldBeEmpty();
    }

    /// <summary>Disposing mid-stream ends the stream with DisconnectedHarnessException, not ObjectDisposedException.</summary>
    [Fact]
    public async Task DisposeMidStreamSurfacesDisconnectedHarnessException()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new InMemoryOmpAdapter(ScriptFixtures.Emit(ScriptFixtures.HappySequence)).CreateSession();
        await session.CreateAsync(ct).ConfigureAwait(true);

        var stream = session.SubmitPromptAsync("prompt", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        try
        {
            (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

            await session.DisposeAsync().ConfigureAwait(true);

            await Should.ThrowAsync<DisconnectedHarnessException>(MoveOnceAsync).ConfigureAwait(true);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(true);
        }

        async Task MoveOnceAsync()
        {
            await stream.MoveNextAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A second submit while a stream is open is rejected: one prompt per session.</summary>
    [Fact]
    public async Task SubmitWhileStreamingIsRejectedAsInvalidTransition()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new InMemoryOmpAdapter(ScriptFixtures.Emit(ScriptFixtures.HappySequence)).CreateSession();
        await session.CreateAsync(ct).ConfigureAwait(true);

        var first = session.SubmitPromptAsync("one", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        try
        {
            (await first.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

            var error = await Should.ThrowAsync<InvalidHarnessTransitionException>(() => ScriptFixtures.ConsumeAsync(session, new List<HarnessEvent>(), ct)).ConfigureAwait(true);
            error.From.ShouldBe(HarnessState.Streaming);
            error.To.ShouldBe(HarnessState.Streaming);
            session.State.ShouldBe(HarnessState.Streaming);
        }
        finally
        {
            await first.DisposeAsync().ConfigureAwait(true);
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Sessions from one adapter are independent: one can stream while another runs a full prompt.</summary>
    [Fact]
    public async Task MultipleSessionsAreIndependent()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = new InMemoryOmpAdapter(ScriptFixtures.Emit(ScriptFixtures.HappySequence));
        var first = adapter.CreateSession();
        var second = adapter.CreateSession();
        await first.CreateAsync(ct).ConfigureAwait(true);
        await second.CreateAsync(ct).ConfigureAwait(true);

        var firstStream = first.SubmitPromptAsync("one", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        try
        {
            (await firstStream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
            firstStream.Current.ShouldBe(ScriptFixtures.HappySequence[0]);
            first.State.ShouldBe(HarnessState.Streaming);
            second.State.ShouldBe(HarnessState.Ready);

            var secondSeen = new List<HarnessEvent>();
            await ScriptFixtures.ConsumeAsync(second, secondSeen, ct).ConfigureAwait(true);
            secondSeen.ShouldBe(ScriptFixtures.HappySequence);

            var firstRest = new List<HarnessEvent>();
            while (await firstStream.MoveNextAsync().ConfigureAwait(true))
            {
                firstRest.Add(firstStream.Current);
            }

            firstRest.ShouldBe(ScriptFixtures.HappySequence.Skip(1));
            first.State.ShouldBe(HarnessState.Ready);
        }
        finally
        {
            await firstStream.DisposeAsync().ConfigureAwait(true);
            await first.DisposeAsync().ConfigureAwait(true);
            await second.DisposeAsync().ConfigureAwait(true);
        }
    }
}
