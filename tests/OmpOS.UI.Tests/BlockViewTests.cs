using System.Text.Json;
using Bunit;
using OmpOS.Contracts.Ui;
using OmpOS.UI.Components.Blocks;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The twelve block views in the web head: every kind renders, every control is named, and a status is a glyph plus words.</summary>
public sealed class BlockViewTests : UiTestContext
{
    private static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static UiBlock[] OneOfEachKind() =>
    [
        new UserMessageBlock { Id = "b-1", Text = "Ship it" },
        new MarkdownBlock { Id = "b-2", Markdown = "hello **world**" },
        new CodeBlock { Id = "b-3", Language = "csharp", Code = "var x = 1;" },
        new ToolCallBlock { Id = "b-4", CallId = "c-1", ToolName = "list_files", Status = "completed", Arguments = Json("{}") },
        new ToolResultBlock { Id = "b-5", CallId = "c-1", ToolName = "list_files", Status = "completed", Output = "3 files" },
        new FileEditBlock { Id = "b-6", CallId = "c-2", Path = "src/a.cs", OldText = "old", NewText = "new" },
        new PermissionBlock { Id = "b-7", Prompt = "Run shell command", Options = ["Allow once", "Deny"] },
        new PlanBlock { Id = "b-8", Steps = ["Read the spec", "Write the parser"] },
        new DiffPlaceholderBlock { Id = "b-9", Path = "src/big.cs", Reason = "large diff withheld" },
        new ErrorBlock { Id = "b-10", Message = "rate limited", Retryable = true },
        new A2uiSurfaceBlock { Id = "b-11", SurfaceId = "s-1", Payload = Json("""{"components":[]}""") },
        new UnknownBlock { Id = "b-12", OriginalKind = "future", Raw = Json("""{"kind":"future"}""") },
    ];

    /// <summary>All twelve kinds render under the shared block class, with no script, no inline style and no unnamed control.</summary>
    [Fact]
    public void EveryKindRendersWithNamedControlsAndNoInlineStyle()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetVoidResult();
        foreach (var block in OneOfEachKind())
        {
            var cut = Render<UiBlockRenderer>(parameters => parameters.Add(p => p.Block, block));

            cut.FindAll(".ompos-block").Count.ShouldBeGreaterThan(0, $"{block.GetType().Name} renders a block");
            cut.FindAll("script").ShouldBeEmpty();
            cut.FindAll("[style]").ShouldBeEmpty($"{block.GetType().Name} has no inline style");
            foreach (var control in cut.FindAll("button, summary"))
            {
                (control.GetAttribute("aria-label") ?? control.TextContent).Trim().ShouldNotBeEmpty($"{block.GetType().Name} has a named control");
            }
        }
    }

    /// <summary>A tool call shows its status as a decorative glyph and as words, never colour alone.</summary>
    [Theory]
    [InlineData("completed", "check")]
    [InlineData("failed", "cross")]
    [InlineData("in_progress", "running")]
    [InlineData("pending", "clock")]
    [InlineData("mystery", "ring")]
    public void AToolCallPairsItsStatusWordsWithAGlyph(string status, string expectedGlyph)
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters.Add(p => p.Block, new ToolCallBlock
        {
            Id = "t-1",
            CallId = "c-1",
            ToolName = "edit",
            Status = status,
            Arguments = Json("{}"),
        }));

        cut.Find(".ompos-tool-status").TextContent.ShouldBe(status);
        var glyph = cut.Find("svg.ompos-glyph");
        glyph.GetAttribute("data-icon").ShouldBe(expectedGlyph);
        glyph.GetAttribute("aria-hidden").ShouldBe("true");
    }

    /// <summary>A file edit offers a diff behind a named toggle; showing it draws the real diff, hiding it removes it.</summary>
    [Fact]
    public async Task AFileEditShowsItsDiffOnRequest()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters.Add(p => p.Block, new FileEditBlock
        {
            Id = "e-1",
            CallId = "c-1",
            Path = "src/a.cs",
            OldText = "one\ntwo\n",
            NewText = "one\nTWO\n",
        }));
        cut.FindAll(".ompos-diff-view").ShouldBeEmpty();
        var toggle = cut.Find("button.ompos-file-edit-diff-toggle");
        toggle.GetAttribute("aria-label").ShouldBe("Show diff for src/a.cs");
        toggle.GetAttribute("aria-expanded").ShouldBe("false");

        await toggle.ClickAsync().ConfigureAwait(true);

        cut.Find("button.ompos-file-edit-diff-toggle").GetAttribute("aria-label").ShouldBe("Hide diff for src/a.cs");
        cut.Find("tr.ompos-diff-line[data-kind='added'] code").TextContent.ShouldBe("TWO");

        await cut.Find("button.ompos-file-edit-diff-toggle").ClickAsync().ConfigureAwait(true);
        cut.FindAll(".ompos-diff-view").ShouldBeEmpty();
    }

    /// <summary>An edit that changed nothing offers no diff.</summary>
    [Fact]
    public void AnUnchangedFileEditOffersNoDiff()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters.Add(p => p.Block, new FileEditBlock
        {
            Id = "e-1",
            CallId = "c-1",
            Path = "src/a.cs",
            OldText = "same",
            NewText = "same",
        }));

        cut.FindAll("button").ShouldBeEmpty();
    }

    /// <summary>After a copy the code block says so in a polite status region, which is empty before.</summary>
    [Fact]
    public async Task ACopiedCodeBlockSaysSo()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetVoidResult();
        var cut = Render<UiBlockRenderer>(parameters => parameters.Add(p => p.Block, new CodeBlock { Id = "c-1", Language = "csharp", Code = "var x = 1;" }));
        cut.Find(".ompos-sr-only[role='status']").TextContent.ShouldBeEmpty();
        cut.Find(".ompos-code-language").TextContent.ShouldBe("csharp");

        await cut.Find("button[aria-label='Copy code']").ClickAsync().ConfigureAwait(true);

        cut.Find(".ompos-sr-only[role='status']").TextContent.ShouldBe("Copied to clipboard");
    }

    /// <summary>An error block is an assertive alert with a named retry control.</summary>
    [Fact]
    public void AnErrorBlockIsAnAlertWithRetry()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters.Add(p => p.Block, new ErrorBlock { Id = "x-1", Message = "boom", Retryable = true }));

        cut.Find(".ompos-error").GetAttribute("role").ShouldBe("alert");
        cut.Find("button").GetAttribute("aria-label").ShouldBe("Retry failed operation");
    }
}
