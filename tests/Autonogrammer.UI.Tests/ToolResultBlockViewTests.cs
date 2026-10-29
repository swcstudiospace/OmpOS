using Autonogrammer.Contracts.Ui;
using Autonogrammer.UI.Components.Blocks;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The web head's rendering of <see cref="ToolResultBlock"/> through <see cref="UiBlockRenderer"/>.</summary>
public sealed class ToolResultBlockViewTests : BunitContext
{
    /// <summary>A tool result shows the tool name, the status and the output text, in a group a screen reader can name.</summary>
    [Fact]
    public void RendersToolNameStatusAndOutput()
    {
        var cut = RenderResult(new ToolResultBlock { Id = "call-1-result", CallId = "call-1", ToolName = "ls", Status = "completed", Output = "3 files" });

        var block = cut.Find(".autonogrammer-tool-result");
        block.GetAttribute("data-status").ShouldBe("completed");
        block.GetAttribute("role").ShouldBe("group");
        block.GetAttribute("aria-label").ShouldBe("Tool result: ls");
        cut.Find(".autonogrammer-tool-name").TextContent.ShouldBe("ls");
        cut.Find(".autonogrammer-tool-status").TextContent.ShouldBe("completed");
        cut.Find(".autonogrammer-tool-output").TextContent.ShouldBe("3 files");
    }

    /// <summary>Output that looks like markup is shown as text: no element is created from it.</summary>
    [Fact]
    public void OutputIsTextNeverMarkup()
    {
        const string Hostile = "<script>alert(1)</script><img src=x onerror=alert(2)>";
        var cut = RenderResult(new ToolResultBlock { Id = "call-1-result", CallId = "call-1", ToolName = "cat", Status = "completed", Output = Hostile });

        cut.Find(".autonogrammer-tool-output").TextContent.ShouldBe(Hostile);
        cut.FindAll("script").ShouldBeEmpty();
        cut.FindAll("img").ShouldBeEmpty();
    }

    /// <summary>Output keeps its line breaks, and the scrollable output can be reached with the keyboard.</summary>
    [Fact]
    public void MultilineOutputKeepsItsLinesAndIsFocusable()
    {
        var cut = RenderResult(new ToolResultBlock { Id = "call-1-result", CallId = "call-1", ToolName = "ls", Status = "completed", Output = "a.txt\nb.txt" });

        var output = cut.Find("pre.autonogrammer-tool-output");
        output.TextContent.ShouldBe("a.txt\nb.txt");
        output.GetAttribute("tabindex").ShouldBe("0");
    }

    private IRenderedComponent<UiBlockRenderer> RenderResult(ToolResultBlock block) =>
        Render<UiBlockRenderer>(parameters => parameters.Add(p => p.Block, block));
}
