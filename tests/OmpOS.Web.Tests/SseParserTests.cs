using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>SSE frame-parser unit tests: split chunks, comments, dispatch rules.</summary>
public sealed class SseParserTests
{
    /// <summary>Frames split across chunks at any byte still parse exactly once.</summary>
    [Fact]
    public void FramesSplitAcrossChunksParseExactlyOnce()
    {
        var parser = new SseParser();
        var frames = new List<SseFrame>();
        const string body = "id: 1\nevent: text_delta\ndata: {\"a\":1}\n\nid: 2\nevent: completed\ndata: {\"b\":2}\n\n";
        foreach (var chunk in ChunkBy(body, 3))
        {
            frames.AddRange(parser.Feed(chunk));
        }

        frames.Count.ShouldBe(2);
        frames[0].ShouldBe(new SseFrame("1", "text_delta", "{\"a\":1}"));
        frames[1].ShouldBe(new SseFrame("2", "completed", "{\"b\":2}"));
    }

    /// <summary>Comment lines (heartbeat colons) are ignored and never dispatch a frame.</summary>
    [Fact]
    public void CommentLinesAreIgnored()
    {
        var parser = new SseParser();
        var frames = parser.Feed(": heartbeat\n: retry 1000\nid: 7\nevent: text_delta\ndata: hi\n\n: trailing\n");

        frames.Count.ShouldBe(1);
        frames[0].ShouldBe(new SseFrame("7", "text_delta", "hi"));
    }

    /// <summary>A blank line with no pending fields dispatches nothing.</summary>
    [Fact]
    public void BlankLineWithNoPendingFieldsDispatchesNothing()
    {
        var parser = new SseParser();
        parser.Feed("\n\n").ShouldBeEmpty();
        parser.Flush().ShouldBeNull();
    }

    /// <summary>Multi-line data blocks join with newlines; a trailing event without a blank line flushes at end-of-stream.</summary>
    [Fact]
    public void MultilineDataJoinsAndTrailingEventFlushes()
    {
        var parser = new SseParser();
        var frames = parser.Feed("data: one\ndata: two\n");
        frames.ShouldBeEmpty();

        var trailing = parser.Flush();
        trailing.ShouldNotBeNull();
        trailing.Data.ShouldBe("one\ntwo");
    }

    /// <summary>A single leading space after the colon is stripped; unknown fields are ignored.</summary>
    [Fact]
    public void FieldSpacingAndUnknownFields()
    {
        var parser = new SseParser();
        var frames = parser.Feed("id:  9\nretry: 5000\nevent:text_delta\ndata:x\n\n");

        frames.Count.ShouldBe(1);
        frames[0].ShouldBe(new SseFrame(" 9", "text_delta", "x"));
    }

    private static IEnumerable<string> ChunkBy(string text, int size)
    {
        for (var i = 0; i < text.Length; i += size)
        {
            yield return text.Substring(i, Math.Min(size, text.Length - i));
        }
    }
}
