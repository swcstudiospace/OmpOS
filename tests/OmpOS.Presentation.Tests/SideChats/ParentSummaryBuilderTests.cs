using OmpOS.Contracts.Ui;
using OmpOS.Presentation.SideChats;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.SideChats;

/// <summary>Verifies the read-only summary a side chat is seeded with.</summary>
public sealed class ParentSummaryBuilderTests
{
    private static UserMessageBlock User(string id, string text) => new() { Id = id, Text = text };

    private static MarkdownBlock Said(string id, string text) => new() { Id = id, Markdown = text };

    [Fact]
    public void TheSummaryListsWhatWasAskedAnsweredPlannedEditedAndFailedInOrder()
    {
        IReadOnlyList<UiBlock> blocks =
        [
            User("u1", "Add a cache"),
            new PlanBlock { Id = "p", Steps = ["read", "write"] },
            ChangeBlocks.Edit("src/a.cs", "a", "b", "c1"),
            ChangeBlocks.Edit("src/a.cs", "b", "c", "c2"),
            ChangeBlocks.Edit("src/b.cs", null, "x", "c3"),
            Said("m1", "Done: cache added."),
            new ErrorBlock { Id = "e", Message = "tests failed", Retryable = true },
            User("u2", "Why did it fail?\nPlease explain."),
        ];

        ParentSummaryBuilder.Build(blocks).ShouldBe(
            "User: Add a cache\nPlan: read; write\nEdited files: src/a.cs, src/b.cs\nAssistant: Done: cache added.\nError: tests failed\nUser: Why did it fail? Please explain.");
    }

    [Fact]
    public void AnEmptyTranscriptHasAnEmptySummaryAndOtherBlocksAreIgnored()
    {
        ParentSummaryBuilder.Build([]).ShouldBeEmpty();
        ParentSummaryBuilder.Build([Said("m", "   "), ChangeBlocks.Call("c", "completed")]).ShouldBeEmpty();
    }

    [Fact]
    public void WhenTheSummaryIsTooLongTheOldestLinesAreDroppedAndTheNewestKept()
    {
        var blocks = Enumerable.Range(1, 30).Select(index => (UiBlock)User("u" + index, "question number " + index + " " + new string('z', 40))).ToList();

        var summary = ParentSummaryBuilder.Build(blocks, 300);

        summary.Length.ShouldBeLessThanOrEqualTo(300);
        summary.ShouldContain("question number 30", Case.Sensitive);
        summary.ShouldNotContain("question number 1 ", Case.Sensitive);
        summary.ShouldStartWith("User:", Case.Sensitive);
    }

    [Fact]
    public void ALongLineIsShortenedAndATinyLimitIsRefused()
    {
        var summary = ParentSummaryBuilder.Build([Said("m", new string('q', 2000))]);

        summary.Length.ShouldBeLessThan(700);
        summary.ShouldEndWith("\u2026", Case.Sensitive);
        Should.Throw<ArgumentOutOfRangeException>(() => ParentSummaryBuilder.Build([], 10));
    }
}
