using Autonogrammer.Contracts.Ui;
using Autonogrammer.UI.Components.Blocks;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The web head's rendering of <see cref="FileEditBlock"/> through <see cref="UiBlockRenderer"/>.</summary>
public sealed class FileEditBlockViewTests : BunitContext
{
    /// <summary>An edit of an existing file shows its path, that it was edited, and the text before and after.</summary>
    [Fact]
    public void RendersPathAndBothTexts()
    {
        var cut = RenderEdit(new FileEditBlock { Id = "call-1-edit-0", CallId = "call-1", Path = "src/a.cs", OldText = "old", NewText = "new" });

        var block = cut.Find(".autonogrammer-file-edit");
        block.GetAttribute("role").ShouldBe("group");
        block.GetAttribute("aria-label").ShouldBe("File edit: src/a.cs");
        cut.Find(".autonogrammer-file-edit-path").TextContent.ShouldBe("src/a.cs");
        cut.Find(".autonogrammer-file-edit-change").TextContent.ShouldBe("edited");
        cut.Find(".autonogrammer-file-edit-before .autonogrammer-file-edit-text").TextContent.ShouldBe("old");
        cut.Find(".autonogrammer-file-edit-after .autonogrammer-file-edit-text").TextContent.ShouldBe("new");
    }

    /// <summary>An edit with no text before it reads as a created file and shows no before section.</summary>
    [Fact]
    public void ACreatedFileShowsOnlyTheAfterText()
    {
        var cut = RenderEdit(new FileEditBlock { Id = "call-1-edit-0", CallId = "call-1", Path = "src/new.cs", OldText = null, NewText = "fresh" });

        cut.Find(".autonogrammer-file-edit-change").TextContent.ShouldBe("created");
        cut.FindAll(".autonogrammer-file-edit-before").ShouldBeEmpty();
        cut.Find(".autonogrammer-file-edit-after .autonogrammer-file-edit-text").TextContent.ShouldBe("fresh");
    }

    /// <summary>A path or file text that looks like markup is shown as text: no element is created from it.</summary>
    [Fact]
    public void PathAndTextsAreTextNeverMarkup()
    {
        const string Hostile = "<script>alert(1)</script>";
        var cut = RenderEdit(new FileEditBlock { Id = "call-1-edit-0", CallId = "call-1", Path = "<b>x</b>.cs", OldText = Hostile, NewText = "<img src=x onerror=alert(2)>" });

        cut.Find(".autonogrammer-file-edit-path").TextContent.ShouldBe("<b>x</b>.cs");
        cut.Find(".autonogrammer-file-edit-before .autonogrammer-file-edit-text").TextContent.ShouldBe(Hostile);
        cut.FindAll("script").ShouldBeEmpty();
        cut.FindAll("img").ShouldBeEmpty();
        cut.FindAll("b").ShouldBeEmpty();
    }

    private IRenderedComponent<UiBlockRenderer> RenderEdit(FileEditBlock block) =>
        Render<UiBlockRenderer>(parameters => parameters.Add(p => p.Block, block));
}
