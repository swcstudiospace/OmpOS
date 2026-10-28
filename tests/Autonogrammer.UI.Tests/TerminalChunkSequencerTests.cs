using Autonogrammer.Presentation.Terminal;
using Autonogrammer.UI.Components.Terminal;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The rules that decide which chunks of an at-least-once terminal stream reach the screen.</summary>
public sealed class TerminalChunkSequencerTests
{
    /// <summary>Chunks that follow each other are accepted and advance the acknowledged sequence.</summary>
    [Fact]
    public void NextChunksAreAcceptedInOrder()
    {
        var sequencer = new TerminalChunkSequencer();

        sequencer.Accept(new RemoteTerminalChunk(1, "a")).ShouldBe(TerminalChunkOutcome.Accepted);
        sequencer.Accept(new RemoteTerminalChunk(2, "b")).ShouldBe(TerminalChunkOutcome.Accepted);

        sequencer.Acked.ShouldBe(2);
    }

    /// <summary>A chunk at or before the acknowledged sequence is a duplicate and changes nothing.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ReplayedChunksAreDuplicates(long sequence)
    {
        var sequencer = new TerminalChunkSequencer();
        sequencer.Accept(new RemoteTerminalChunk(1, "a"));
        sequencer.Accept(new RemoteTerminalChunk(2, "b"));

        sequencer.Accept(new RemoteTerminalChunk(sequence, "again")).ShouldBe(TerminalChunkOutcome.Duplicate);

        sequencer.Acked.ShouldBe(2);
    }

    /// <summary>A chunk that skips ahead is refused as a gap, and the acknowledged sequence stays where it was so a reattach resumes there.</summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    [InlineData(1, 100)]
    public void SkippedAheadChunksAreGaps(int accepted, long sequence)
    {
        var sequencer = new TerminalChunkSequencer();
        for (var next = 1; next <= accepted; next++)
        {
            sequencer.Accept(new RemoteTerminalChunk(next, "x"));
        }

        sequencer.Accept(new RemoteTerminalChunk(sequence, "late")).ShouldBe(TerminalChunkOutcome.Gap);

        sequencer.Acked.ShouldBe(accepted);
    }

    /// <summary>A missing chunk is an error, not a silent skip.</summary>
    [Fact]
    public void ANullChunkIsRefused()
    {
        Should.Throw<ArgumentNullException>(() => new TerminalChunkSequencer().Accept(null!));
    }
}
