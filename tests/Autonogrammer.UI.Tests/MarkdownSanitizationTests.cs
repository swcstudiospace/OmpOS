using Autonogrammer.Contracts.Ui;
using Autonogrammer.UI.Components.Blocks;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>Markdown renders as sanitized HTML: no raw HTML, no dangerous hrefs, no remote images (UI-01).</summary>
public sealed class MarkdownSanitizationTests : BunitContext
{
    /// <summary>Emphasis renders a strong element.</summary>
    [Fact]
    public void BoldRendersStrong()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new MarkdownBlock { Id = "m-1", Markdown = "hello **b**" }));

        cut.Find("strong").TextContent.ShouldBe("b");
    }

    /// <summary>Raw script source is escaped and never becomes a script element.</summary>
    [Fact]
    public void ScriptSourceIsEscaped()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new MarkdownBlock { Id = "m-1", Markdown = "<script>alert(1)</script>" }));

        cut.FindAll("script").Count.ShouldBe(0);
        cut.Markup.ShouldContain("&lt;script&gt;");
    }

    /// <summary>Anchor hrefs keep only http, https, mailto, and relative references.</summary>
    /// <param name="target">The markdown link target.</param>
    /// <param name="expectedHref">The kept href, or null when it must be dropped.</param>
    [Theory]
    [InlineData("https://example.com/", "https://example.com/")]
    [InlineData("http://example.com/", "http://example.com/")]
    [InlineData("mailto:someone@example.com", "mailto:someone@example.com")]
    [InlineData("/docs/local", "/docs/local")]
    [InlineData("relative/path", "relative/path")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("data:text/html,<b>x</b>", null)]
    [InlineData("vbscript:msgbox(1)", null)]
    [InlineData("file:///etc/passwd", null)]
    public void LinkHrefAllowlist(string target, string? expectedHref)
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new MarkdownBlock { Id = "m-1", Markdown = $"[x]({target})" }));

        var link = cut.Find("a");
        link.TextContent.ShouldBe("x");
        link.GetAttribute("href").ShouldBe(expectedHref);
    }

    /// <summary>A fenced code block renders inside pre and code elements.</summary>
    [Fact]
    public void FencedCodeRendersPreAndCode()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new MarkdownBlock { Id = "m-1", Markdown = "```csharp\nvar x = 1;\n```" }));

        var code = cut.Find("pre code");
        code.TextContent.ShouldContain("var x = 1;");
    }

    /// <summary>A remote markdown image renders no img element: a fallback link carries the alt text.</summary>
    [Fact]
    public void HttpsImageRendersFallbackLink()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new MarkdownBlock { Id = "m-1", Markdown = "![alt text](https://example.com/i.png)" }));

        cut.FindAll("img").Count.ShouldBe(0);
        var link = cut.Find("a");
        link.TextContent.ShouldBe("alt text");
        link.GetAttribute("href").ShouldBe("https://example.com/i.png");
        link.GetAttribute("rel").ShouldBe("noopener noreferrer nofollow");
        link.GetAttribute("target").ShouldBe("_blank");
    }

    /// <summary>An inline PNG data image keeps its img element.</summary>
    [Fact]
    public void DataPngImageRendersImg()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new MarkdownBlock { Id = "m-1", Markdown = "![alt text](data:image/png;base64,iVBORw0KGgo=)" }));

        var image = cut.Find("img");
        image.GetAttribute("src").ShouldStartWith("data:image/png");
        image.GetAttribute("alt").ShouldBe("alt text");
    }

    /// <summary>An inline SVG data image renders no img element.</summary>
    [Fact]
    public void DataSvgImageRendersNoImg()
    {
        var cut = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new MarkdownBlock { Id = "m-1", Markdown = "![alt text](data:image/svg+xml;base64,PHN2Zz4=)" }));

        cut.FindAll("img").Count.ShouldBe(0);
        cut.Find("a").TextContent.ShouldBe("alt text");
    }
}
