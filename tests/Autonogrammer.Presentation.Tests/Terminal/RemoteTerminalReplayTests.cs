using Autonogrammer.Presentation.Terminal;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Terminal;

/// <summary>The remote-attached terminal keeps one ordered copy of the output across duplicates and network loss (phase 43, VX-08).</summary>
public sealed class RemoteTerminalReplayTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RemoteTerminalChunk Chunk(long sequence, string text) => new(sequence, text);

    /// <summary>Chunks the transport delivers twice or out of date are shown once, in order.</summary>
    [Fact]
    public async Task RemoteAttachedTerminalReplaysWithoutDuplicates()
    {
        var transport = new FakeRemoteTerminalTransport();
        transport.Enqueue(false, Chunk(1, "a"), Chunk(2, "b"), Chunk(2, "b"), Chunk(1, "a"), Chunk(3, "c"));
        var surface = new TerminalSurfaceModel(TerminalHostKind.Mobile, transport);

        await surface.AttachRemoteAsync("t-1", Token).ConfigureAwait(true);

        surface.Output.ShouldBe("abc");
        surface.AckedSequence.ShouldBe(3);
    }

    /// <summary>After the connection breaks, attaching again asks for output after the last accepted chunk and does not repeat what was shown.</summary>
    [Fact]
    public async Task ReattachAfterNetworkLossResumesFromLastAckedSequence()
    {
        var transport = new FakeRemoteTerminalTransport();
        transport.Enqueue(true, Chunk(1, "one "), Chunk(2, "two "), Chunk(3, "three "));
        transport.Enqueue(false, Chunk(3, "three "), Chunk(4, "four "), Chunk(5, "five"));
        var surface = new TerminalSurfaceModel(TerminalHostKind.Web, transport);

        await surface.AttachRemoteAsync("t-1", Token).ConfigureAwait(true);
        surface.State.ShouldBe(RemoteTerminalState.ConnectionLost);
        surface.AckedSequence.ShouldBe(3);

        await surface.AttachRemoteAsync("t-1", Token).ConfigureAwait(true);

        transport.AttachRequests.ShouldBe([0, 3]);
        surface.Output.ShouldBe("one two three four five");
        surface.AckedSequence.ShouldBe(5);
    }

    /// <summary>A chunk that skips ahead is not shown; the attach ends as lost so the next attach replays the missing output.</summary>
    [Fact]
    public async Task SkippedChunkEndsTheAttachWithoutShowingOutOfOrderOutput()
    {
        var transport = new FakeRemoteTerminalTransport();
        transport.Enqueue(false, Chunk(1, "a"), Chunk(3, "c"));
        transport.Enqueue(false, Chunk(2, "b"), Chunk(3, "c"));
        var surface = new TerminalSurfaceModel(TerminalHostKind.Mobile, transport);

        await surface.AttachRemoteAsync("t-1", Token).ConfigureAwait(true);

        surface.Output.ShouldBe("a");
        surface.State.ShouldBe(RemoteTerminalState.ConnectionLost);

        await surface.AttachRemoteAsync("t-1", Token).ConfigureAwait(true);

        transport.AttachRequests.ShouldBe([0, 1]);
        surface.Output.ShouldBe("abc");
    }

    /// <summary>Output is announced as it grows so a bound terminal redraws.</summary>
    [Fact]
    public async Task OutputAndAckAreAnnounced()
    {
        var transport = new FakeRemoteTerminalTransport();
        transport.Enqueue(false, Chunk(1, "x"));
        var surface = new TerminalSurfaceModel(TerminalHostKind.Web, transport);
        List<string> changed = PropertyChangeRecorder.Record(surface);

        await surface.AttachRemoteAsync("t-1", Token).ConfigureAwait(true);

        changed.ShouldAnnounce(nameof(TerminalSurfaceModel.Output));
        changed.ShouldAnnounce(nameof(TerminalSurfaceModel.AckedSequence));
        changed.ShouldAnnounce(nameof(TerminalSurfaceModel.State));
    }

    /// <summary>A cancelled attach detaches and propagates the cancellation, keeping what was already accepted.</summary>
    [Fact]
    public async Task CancelledAttachDetachesAndKeepsAcceptedOutput()
    {
        var transport = new FakeRemoteTerminalTransport();
        transport.Enqueue(false, Chunk(1, "a"), Chunk(2, "b"));
        var surface = new TerminalSurfaceModel(TerminalHostKind.Web, transport);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => surface.AttachRemoteAsync("t-1", cts.Token)).ConfigureAwait(true);

        surface.State.ShouldBe(RemoteTerminalState.Detached);
        surface.Output.ShouldBeEmpty();
    }
}
