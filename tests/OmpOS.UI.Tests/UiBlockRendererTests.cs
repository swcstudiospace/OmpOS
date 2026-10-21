using System.Text.Json;
using Bunit;
using OmpOS.Contracts.Ui;
using OmpOS.UI.Components.Blocks;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>One render assertion per <see cref="UiBlock"/> variant (UI-01).</summary>
internal sealed class UiBlockRendererTests : BunitContext
{
    /// <summary>A markdown block renders its text as HTML.</summary>
    [Fact]
    public void RendersMarkdownText()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new MarkdownBlock { Id = "m-1", Markdown = "hello **world**" }));

        cut.Find(".ompos-markdown").ShouldNotBeNull();
        cut.Find("strong").TextContent.ShouldBe("world");
        cut.Markup.ShouldNotContain("**world**");
    }

    /// <summary>A code block renders its code with a labeled copy control.</summary>
    [Fact]
    public void RendersCodeBlockWithCopyControl()
    {
        var copied = new List<string>();
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetVoidResult();
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new CodeBlock { Id = "c-1", Language = "csharp", Code = "var x = 1;" })
            .Add(p => p.OnCopy, (string code) => copied.Add(code)));

        cut.Markup.ShouldContain("var x = 1;");
        cut.Markup.ShouldContain("csharp");
        var copy = cut.Find("button[aria-label=\"Copy code\"]");
        copy.ShouldNotBeNull();
        copy.Click();
        cut.WaitForAssertion(() => copied.ShouldBe(["var x = 1;"]));
        JSInterop.VerifyInvoke("navigator.clipboard.writeText");
    }

    /// <summary>A tool-call card renders the tool name and status.</summary>
    [Fact]
    public void RendersToolCallCardWithStatus()
    {
        using var args = JsonDocument.Parse("{\"path\":\"a.txt\"}");
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new ToolCallBlock
            {
                Id = "call-1",
                CallId = "call-1",
                ToolName = "read",
                Status = "completed",
                Arguments = args.RootElement.Clone(),
            }));

        cut.Markup.ShouldContain("read");
        cut.Markup.ShouldContain("completed");
    }

    /// <summary>Permission options render verbatim, in agent-provided order, read-only with the policy note.</summary>
    [Fact]
    public void RendersPermissionOptionsVerbatimReadOnly()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new PermissionBlock { Id = "p-1", Prompt = "Run bash?", Options = ["allow", "deny", "allow always"] }));

        cut.Markup.ShouldContain("Run bash?");
        var options = cut.FindAll(".ompos-permission-options .ompos-permission-option");
        options.Select(static option => option.TextContent).ShouldBe(["allow", "deny", "allow always"]);
        cut.FindAll(".ompos-permission-options button").Count.ShouldBe(0);
        cut.Markup.ShouldContain("The decision is made by policy.");
    }

    /// <summary>A permission block without an outcome in the payload never claims an approval or denial.</summary>
    [Fact]
    public void PermissionBlockShowsNoOutcomeWithoutPayloadOutcome()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new PermissionBlock { Id = "p-1", Prompt = "Run bash?", Options = ["allow once", "ask me"] }));

        cut.Markup.ShouldNotContain("approved");
        cut.Markup.ShouldNotContain("Approved");
        cut.Markup.ShouldNotContain("denied");
        cut.Markup.ShouldNotContain("Denied");
    }

    /// <summary>A plan block renders its steps as an ordered list.</summary>
    [Fact]
    public void RendersPlanPanel()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new PlanBlock { Id = "plan-1", Steps = ["read", "answer"] }));

        var items = cut.FindAll(".ompos-plan li");
        items.Select(static item => item.TextContent).ShouldBe(["read", "answer"]);
    }

    /// <summary>A diff placeholder renders its path and reason.</summary>
    [Fact]
    public void RendersDiffPlaceholder()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new DiffPlaceholderBlock { Id = "d-1", Path = "src/Foo.cs", Reason = "large diff withheld" }));

        cut.Markup.ShouldContain("src/Foo.cs");
        cut.Markup.ShouldContain("large diff withheld");
    }

    /// <summary>An error block renders its message, with retry only when retryable.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RendersErrorBlock(bool retryable)
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new ErrorBlock { Id = "e-1", Message = "boom", Retryable = retryable }));

        cut.Markup.ShouldContain("boom");
        cut.FindAll("button[aria-label=\"Retry failed operation\"]").Count.ShouldBe(retryable ? 1 : 0);
    }

    /// <summary>The A2UI subset renders heading, text, image, button, and list components.</summary>
    [Fact]
    public void RendersA2uiSubset()
    {
        using var payload = JsonDocument.Parse("""
            {"components": [
              {"type": "heading", "text": "Title"},
              {"type": "text", "text": "Body"},
              {"type": "image", "url": "data:image/png;base64,iVBORw0KGgo=", "alt": "An image"},
              {"type": "button", "label": "Press"},
              {"type": "list", "items": ["a", "b"]}
            ]}
            """);
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new A2uiSurfaceBlock { Id = "a-1", SurfaceId = "s-1", Payload = payload.RootElement.Clone() }));

        cut.Markup.ShouldContain("Title");
        cut.Markup.ShouldContain("Body");
        cut.Markup.ShouldContain("Press");
        cut.Find("img[alt=\"An image\"]").ShouldNotBeNull();
        cut.FindAll(".ompos-a2ui-list li").Select(static item => item.TextContent).ShouldBe(["a", "b"]);
    }

    /// <summary>A remote A2UI image renders no img element: a fallback link carries the alt text.</summary>
    [Fact]
    public void RemoteA2uiImageRendersFallbackLink()
    {
        using var payload = JsonDocument.Parse("""
            {"components": [
              {"type": "image", "url": "https://example.com/i.png", "alt": "An image"}
            ]}
            """);
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new A2uiSurfaceBlock { Id = "a-1", SurfaceId = "s-1", Payload = payload.RootElement.Clone() }));

        cut.FindAll("img").Count.ShouldBe(0);
        var link = cut.Find("a.ompos-a2ui-image-link");
        link.TextContent.ShouldBe("An image");
        link.GetAttribute("href").ShouldBe("https://example.com/i.png");
        link.GetAttribute("rel").ShouldBe("noopener noreferrer nofollow");
        link.GetAttribute("target").ShouldBe("_blank");
    }

    /// <summary>An unknown block renders as a collapsed disclosure showing the raw JSON.</summary>
    [Fact]
    public void RendersUnknownCollapsed()
    {
        using var raw = JsonDocument.Parse("{\"kind\":\"future\",\"n\":1}");
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new UnknownBlock { Id = "u-1", OriginalKind = "future", Raw = raw.RootElement.Clone() }));

        var details = cut.Find("details.ompos-unknown");
        details.ShouldNotBeNull();
        details.GetAttribute("open").ShouldBeNull();
        cut.Markup.ShouldContain("future");
    }
}
