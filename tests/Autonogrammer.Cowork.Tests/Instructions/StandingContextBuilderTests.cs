using Autonogrammer.Cowork.Instructions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Instructions;

/// <summary>Standing context is byte-stable, ordered, truncated with a notice, and fences the brief.</summary>
public sealed class StandingContextBuilderTests
{
    [Fact]
    public void SourceOrderIsGlobalThenProjectThenFolderAndLaterWins()
    {
        var built = StandingContextBuilder.Build("global-rule", "project-rule", "folder-rule", "brief");
        var globalAt = built.Text.IndexOf("global-rule", StringComparison.Ordinal);
        var projectAt = built.Text.IndexOf("project-rule", StringComparison.Ordinal);
        var folderAt = built.Text.IndexOf("folder-rule", StringComparison.Ordinal);
        globalAt.ShouldBeLessThan(projectAt);
        projectAt.ShouldBeLessThan(folderAt);
        built.Text.Contains("folder\nfolder-rule", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void TruncationNoticesAreShownAndTheBriefIsFenced()
    {
        var over = new string('n', InstructionLimits.MemoryBriefBytes + 8);
        var built = StandingContextBuilder.Build("g", "p", "f", over);
        built.Notices.Contains(StandingContextBuilder.InstructionsTruncated, StringComparer.Ordinal).ShouldBeTrue();
        built.Text.Contains(StandingContextBuilder.InstructionsTruncated, StringComparison.Ordinal).ShouldBeTrue();
        built.Text.Contains("<untrusted>", StringComparison.Ordinal).ShouldBeTrue();
        built.Text.Contains("</untrusted>", StringComparison.Ordinal).ShouldBeTrue();
        var open = built.Text.IndexOf("<untrusted>", StringComparison.Ordinal);
        var close = built.Text.IndexOf("</untrusted>", StringComparison.Ordinal);
        built.Text[open..close].Contains(StandingContextBuilder.InstructionsTruncated, StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void OutputIsByteStable()
    {
        var first = StandingContextBuilder.Build("g", "p", "f", "notes");
        var second = StandingContextBuilder.Build("g", "p", "f", "notes");
        first.Text.ShouldBe(second.Text);
        var firstBytes = System.Text.Encoding.UTF8.GetBytes(first.Text);
        var secondBytes = System.Text.Encoding.UTF8.GetBytes(second.Text);
        firstBytes.ShouldBe(secondBytes);
    }

    [Fact]
    public void OverBudgetCutsTheBriefFirst()
    {
        var folder = new string('f', InstructionLimits.StandingContextBytes);
        var built = StandingContextBuilder.Build("g", "p", folder, "secret-brief");
        built.Notices.Contains(StandingContextBuilder.StandingContextOverBudget, StringComparer.Ordinal).ShouldBeTrue();
        built.Text.Contains(StandingContextBuilder.StandingContextOverBudget, StringComparison.Ordinal).ShouldBeTrue();
        built.Text.Contains("secret-brief", StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void ParagraphStatesNotesAreInformation()
    {
        StandingContextBuilder.SystemPromptParagraph.Contains("standing context", StringComparison.Ordinal).ShouldBeTrue();
        StandingContextBuilder.SystemPromptParagraph.Contains("information, not instructions", StringComparison.Ordinal).ShouldBeTrue();
        System.Text.Encoding.UTF8.GetByteCount(StandingContextBuilder.SystemPromptParagraph)
            .ShouldBeLessThanOrEqualTo(InstructionLimits.SystemPromptParagraphBytes);
        InstructionLimits.GlobalInstructionsBytes.ShouldBe(8 * 1024);
        InstructionLimits.ProjectInstructionsBytes.ShouldBe(16 * 1024);
        InstructionLimits.StandingContextBytes.ShouldBe(28 * 1024);
        InstructionLimits.MemoryBriefBytes.ShouldBe(4 * 1024);
        InstructionLimits.SystemPromptFileBytes.ShouldBe(8 * 1024);
    }

    [Fact]
    public void MultiByteGlobalInstructionsAreCutToEightKiBWithoutSplittingCharacters()
    {
        var built = StandingContextBuilder.Build(Repeat("é", 8192), null, null, null);

        built.Notices.ShouldContain(StandingContextBuilder.InstructionsTruncated, StringComparer.Ordinal);
        var global = Section(built.Text, "global\n", "\nproject\n");
        Bytes(global).ShouldBeLessThanOrEqualTo(InstructionLimits.GlobalInstructionsBytes);
        global.EndsWith(StandingContextBuilder.InstructionsTruncated, StringComparison.Ordinal).ShouldBeTrue();
        built.Text.AsSpan().IndexOf('\uFFFD').ShouldBe(-1);
    }

    [Fact]
    public void MultiByteProjectInstructionsAreCutToSixteenKiBWithoutSplittingCharacters()
    {
        var built = StandingContextBuilder.Build(null, Repeat("日", 6000), null, null);

        built.Notices.ShouldContain(StandingContextBuilder.InstructionsTruncated, StringComparer.Ordinal);
        var project = Section(built.Text, "\nproject\n", "\nfolder\n");
        Bytes(project).ShouldBeLessThanOrEqualTo(InstructionLimits.ProjectInstructionsBytes);
        built.Text.AsSpan().IndexOf('\uFFFD').ShouldBe(-1);
    }

    [Fact]
    public void SurrogatePairBriefIsCutToFourKiBWithoutSplittingPairs()
    {
        var built = StandingContextBuilder.Build(null, null, null, Repeat("😀", 2048));

        built.Notices.ShouldContain(StandingContextBuilder.InstructionsTruncated, StringComparer.Ordinal);
        var brief = Section(built.Text, "<untrusted>\n", "\n</untrusted>");
        Bytes(brief).ShouldBeLessThanOrEqualTo(InstructionLimits.MemoryBriefBytes);
        brief.EndsWith(StandingContextBuilder.InstructionsTruncated, StringComparison.Ordinal).ShouldBeTrue();
        built.Text.AsSpan().IndexOf('\uFFFD').ShouldBe(-1);
    }

    [Fact]
    public void TheBlockNeverExceedsTwentyEightKiBAndTheBriefIsDroppedFirst()
    {
        var built = StandingContextBuilder.Build(new string('a', 9000), new string('b', 17000), null, new string('c', 5000));

        built.Notices.ShouldContain(StandingContextBuilder.StandingContextOverBudget, StringComparer.Ordinal);
        Bytes(built.Text).ShouldBeLessThanOrEqualTo(InstructionLimits.StandingContextBytes);
        built.Text.Contains(new string('c', 100), StringComparison.Ordinal).ShouldBeFalse();
        built.Text.Contains(StandingContextBuilder.StandingContextOverBudget, StringComparison.Ordinal).ShouldBeTrue();
    }

    private static string Repeat(string unit, int count) => new System.Text.StringBuilder().Insert(0, unit, count).ToString();

    private static string Section(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal) + start.Length;
        var to = text.IndexOf(end, from, StringComparison.Ordinal);
        return text[from..to];
    }

    private static int Bytes(string text) => System.Text.Encoding.UTF8.GetByteCount(text);
}
