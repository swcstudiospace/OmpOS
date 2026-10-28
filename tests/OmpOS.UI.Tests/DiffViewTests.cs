using Bunit;
using OmpOS.Presentation.Changes;
using OmpOS.UI.Components.Blocks;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The diff view draws a real diff: unified or side by side, with line numbers, a sign and a hidden word for every changed line.</summary>
public sealed class DiffViewTests : UiTestContext
{
    private const string Old = "a\nb\nc\n";
    private const string New = "a\nB\nc\nd\n";

    private IRenderedComponent<DiffView> RenderDiff(string oldText, string newText, DiffViewMode mode = DiffViewMode.Unified, bool showSwitch = true) =>
        Render<DiffView>(parameters => parameters
            .Add(p => p.Diff, ChangeDiff.Create(oldText, newText))
            .Add(p => p.Path, "src/a.cs")
            .Add(p => p.Mode, mode)
            .Add(p => p.ShowModeSwitch, showSwitch));

    /// <summary>The unified layout lists every line of the hunk in order, with its kind, sign and line numbers.</summary>
    [Fact]
    public void UnifiedLayoutListsContextRemovedAndAddedLines()
    {
        var cut = RenderDiff(Old, New);

        var rows = cut.FindAll("tr.ompos-diff-line");
        rows.Select(static row => row.GetAttribute("data-kind")).ShouldBe(["context", "removed", "added", "context", "added"]);
        rows.Select(static row => row.QuerySelector(".ompos-diff-sign")!.TextContent).ShouldBe([" ", "-", "+", " ", "+"]);
        rows.Select(static row => row.QuerySelector("code")!.TextContent).ShouldBe(["a", "b", "B", "c", "d"]);
        rows.Select(static row => string.Concat(row.QuerySelectorAll(".ompos-diff-num").Select(static cell => cell.TextContent + "/"))).ShouldBe(["1/1/", "2//", "/2/", "3/3/", "/4/"]);
        rows[1].QuerySelector(".ompos-sr-only")!.TextContent.ShouldBe("Removed: ");
        rows[2].QuerySelector(".ompos-sr-only")!.TextContent.ShouldBe("Added: ");
        cut.Find(".ompos-diff-summary").TextContent.ShouldBe("+2 -1");
        cut.Find("caption").TextContent.ShouldStartWith("@@ -1,3 +1,4 @@", Case.Sensitive);
    }

    /// <summary>The side-by-side layout pairs a removed line with the added line that replaces it and leaves the other side empty when there is none.</summary>
    [Fact]
    public void SideBySideLayoutPairsRemovedWithAddedLines()
    {
        var cut = RenderDiff(Old, New, DiffViewMode.SideBySide);

        var rows = cut.FindAll("tr.ompos-diff-line");
        rows.Count.ShouldBe(4);
        var kinds = rows.Select(static row => row.QuerySelectorAll(".ompos-diff-text").Select(static cell => cell.GetAttribute("data-kind")).ToArray()).ToArray();
        kinds[0].ShouldBe(["context", "context"]);
        kinds[1].ShouldBe(["removed", "added"]);
        kinds[2].ShouldBe(["context", "context"]);
        kinds[3].ShouldBe(["empty", "added"]);
        rows[1].QuerySelectorAll("code").Select(static code => code.TextContent).ShouldBe(["b", "B"]);
    }

    /// <summary>The layout switch is a pressed-state toggle group, and choosing a layout redraws the table.</summary>
    [Fact]
    public async Task TheLayoutSwitchChangesTheLayout()
    {
        var cut = RenderDiff(Old, New);
        var buttons = cut.FindAll(".ompos-segmented button");
        buttons.Select(static button => button.GetAttribute("aria-pressed")).ShouldBe(["true", "false"]);

        await buttons[1].ClickAsync().ConfigureAwait(true);

        cut.Find(".ompos-diff-table").GetAttribute("data-mode").ShouldBe("sidebyside");
        cut.FindAll(".ompos-segmented button").Select(static button => button.GetAttribute("aria-pressed")).ShouldBe(["false", "true"]);
    }

    /// <summary>A review panel hides the switch; texts that are equal show no hunk and say so.</summary>
    [Fact]
    public void NoSwitchAndNoDifferencesAreHandled()
    {
        var cut = RenderDiff("same\n", "same\n", showSwitch: false);

        cut.FindAll(".ompos-segmented").ShouldBeEmpty();
        cut.Find(".ompos-diff-empty").TextContent.ShouldBe("No differences.");
        cut.FindAll("table").ShouldBeEmpty();
    }

    /// <summary>Line text that looks like markup is shown as text, and the scrollable diff is a named, focusable group with a header row for the table.</summary>
    [Fact]
    public void LineTextIsNeverMarkupAndTheTableIsLabelled()
    {
        var cut = RenderDiff("x\n", "<script>alert(1)</script>\n");

        cut.FindAll("script").ShouldBeEmpty();
        cut.Find("tr.ompos-diff-line[data-kind='added'] code").TextContent.ShouldBe("<script>alert(1)</script>");
        var scroll = cut.Find(".ompos-diff-scroll");
        scroll.GetAttribute("aria-label").ShouldBe("Diff of src/a.cs");
        scroll.GetAttribute("tabindex").ShouldBe("0");
        cut.FindAll("thead th[scope='col']").Count.ShouldBe(4);
    }

    /// <summary>A diff beyond the line cap is cut and says so.</summary>
    [Fact]
    public void ALargeDiffIsCutAndAnnounced()
    {
        var before = string.Join('\n', Enumerable.Range(0, 1100).Select(static index => "line " + index)) + "\n";
        var after = string.Join('\n', Enumerable.Range(0, 1100).Select(static index => "LINE " + index)) + "\n";

        var cut = RenderDiff(before, after);

        cut.FindAll("tr.ompos-diff-line").Count.ShouldBe(DiffView.MaxLines);
        cut.Find(".ompos-diff-truncated").TextContent.ShouldContain("only the first 2000 lines", Case.Sensitive);
    }
}
