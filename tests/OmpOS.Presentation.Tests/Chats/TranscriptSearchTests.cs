using System.Text.Json;
using OmpOS.Contracts.Ui;
using OmpOS.GenUi;
using OmpOS.Presentation.Chats;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Chats;

/// <summary>Transcript search as a pure function over a fixed transcript: match order, offsets, case, and empty queries (ADE-04).</summary>
public sealed class TranscriptSearchTests
{
    /// <summary>A fixed transcript: a markdown block, a code block, a user turn, an error, and a streaming tail.</summary>
    private static UiTree Fixture() => new()
    {
        Blocks =
        [
            new MarkdownBlock { Id = "m-0", Markdown = "Alpha beta ALPHA" },
            new CodeBlock { Id = "c-1", Language = "csharp", Code = "var alpha = 1;" },
            new UserMessageBlock { Id = "u-2", Text = "Tell me about beta" },
            new ErrorBlock { Id = "e-3", Message = "beta failed", Retryable = true },
        ],
        StreamingText = "last Alpha",
    };

    /// <summary>Every match is returned in transcript order with its block index, offset and length; the streaming tail is last.</summary>
    [Fact]
    public void AlphaMatchesEveryBlockInOrderIncludingTheStreamingTail()
    {
        var matches = TranscriptSearch.Find(Fixture(), "alpha");

        matches.ShouldBe(
        [
            new TranscriptMatch(0, 0, 5),
            new TranscriptMatch(0, 11, 5),
            new TranscriptMatch(1, 4, 5),
            new TranscriptMatch(4, 5, 5),
        ]);
    }

    /// <summary>A term that appears in user, markdown and error blocks is found in each, in block order.</summary>
    [Fact]
    public void BetaMatchesAcrossBlockKinds()
    {
        TranscriptSearch.Find(Fixture(), "beta").ShouldBe(
        [
            new TranscriptMatch(0, 6, 4),
            new TranscriptMatch(2, 14, 4),
            new TranscriptMatch(3, 0, 4),
        ]);
    }

    /// <summary>Matching ignores case: upper, lower and mixed queries give the same matches.</summary>
    [Fact]
    public void MatchingIsCaseInsensitive()
    {
        var expected = TranscriptSearch.Find(Fixture(), "alpha");

        TranscriptSearch.Find(Fixture(), "ALPHA").ShouldBe(expected);
        TranscriptSearch.Find(Fixture(), "AlPhA").ShouldBe(expected);
    }

    /// <summary>An empty query matches nothing, even though every block has text.</summary>
    [Fact]
    public void EmptyQueryMatchesNothing()
    {
        TranscriptSearch.Find(Fixture(), string.Empty).ShouldBeEmpty();
    }

    /// <summary>A query that occurs nowhere matches nothing.</summary>
    [Fact]
    public void QueryWithNoOccurrenceMatchesNothing()
    {
        TranscriptSearch.Find(Fixture(), "gamma").ShouldBeEmpty();
    }

    /// <summary>Matches inside one block do not overlap: after a match the scan resumes past it.</summary>
    [Fact]
    public void MatchesWithinOneBlockDoNotOverlap()
    {
        var transcript = new UiTree
        {
            Blocks = [new MarkdownBlock { Id = "m-0", Markdown = "aaaa" }],
        };

        TranscriptSearch.Find(transcript, "aa").ShouldBe([new TranscriptMatch(0, 0, 2), new TranscriptMatch(0, 2, 2)]);
    }

    /// <summary>A permission block's prompt and options are searched as one text, so an option is found at its own offset.</summary>
    [Fact]
    public void PermissionOptionsAreSearchedAfterThePrompt()
    {
        var transcript = new UiTree
        {
            Blocks = [new PermissionBlock { Id = "p-0", Prompt = "bash", Options = ["allow once", "ask me"] }],
        };

        TranscriptSearch.Find(transcript, "once").ShouldBe([new TranscriptMatch(0, "bash\nallow ".Length, 4)]);
    }

    /// <summary>An unknown block carries raw JSON that is not transcript text, so it is never searched.</summary>
    [Fact]
    public void UnknownBlocksAreNotSearched()
    {
        using var doc = JsonDocument.Parse("""{"kind":"mystery","note":"alpha"}""");
        var transcript = new UiTree
        {
            Blocks = [new UnknownBlock { Id = "u-0", OriginalKind = "mystery", Raw = doc.RootElement.Clone() }],
        };

        TranscriptSearch.Find(transcript, "alpha").ShouldBeEmpty();
    }

    /// <summary>A null query is a caller error, not an empty search.</summary>
    [Fact]
    public void NullQueryIsRejected()
    {
        Should.Throw<ArgumentNullException>(() => TranscriptSearch.Find(Fixture(), null!));
    }
}
