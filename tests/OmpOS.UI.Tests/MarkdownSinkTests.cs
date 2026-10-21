using Bunit;
using Microsoft.AspNetCore.Components;
using OmpOS.Contracts.Ui;
using OmpOS.UI.Components.Blocks;
using OmpOS.UI.Services;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The MarkupString sink receives only MarkdownRenderer output, so hostile markdown never reaches the DOM as executable markup (IN-01, TD-37).</summary>
public sealed class MarkdownSinkTests : BunitContext
{
    /// <summary>Hostile markdown renders exactly the MarkdownRenderer output and carries no script, handler attribute, or javascript: href.</summary>
    /// <param name="markdown">The hostile markdown source.</param>
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("[click](javascript:alert(1))")]
    [InlineData("![pic](javascript:alert(1))")]
    [InlineData("<div onmouseover=\"alert(1)\">raw block</div>")]
    [InlineData("<a href=\"javascript:alert(1)\">raw link</a>")]
    [InlineData("<script>alert(1)</script>\n\n<img src=x onerror=alert(1)>\n\n[click](javascript:alert(1))\n\n<div onmouseover=\"alert(1)\">raw block</div>")]
    public void HostileMarkdownRendersAsMarkdownRendererOutput(string markdown)
    {
        var cut = Render<MarkdownBlockView>(parameters => parameters
            .Add(p => p.Block, new MarkdownBlock { Id = "m-1", Markdown = markdown }));
        var html = MarkdownRenderer.ToHtml(markdown);
        RenderFragment expectedFragment = builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddMarkupContent(1, html);
            builder.CloseElement();
        };
        var expected = Render(expectedFragment).Find("div");

        cut.Find("div.ompos-markdown").InnerHtml.ShouldBe(expected.InnerHtml);
        cut.FindAll("script").ShouldBeEmpty();
        cut.FindAll("img").ShouldBeEmpty();
        cut.FindAll("[onerror]").ShouldBeEmpty();
        cut.FindAll("[onmouseover]").ShouldBeEmpty();
        cut.FindAll("[href]")
            .Where(link => (link.GetAttribute("href") ?? string.Empty).TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty();
    }
}
