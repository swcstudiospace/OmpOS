using System.Text.Json;
using Bunit;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.UI.Components.Blocks;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>An A2UI image always has a text alternative, even when the agent's JSON omits <c>alt</c> (phase 25.7, M-25).</summary>
public sealed class A2uiImageAltTests : BunitContext
{
    private const string InlinePng = "data:image/png;base64,iVBORw0KGgo=";

    /// <summary>A missing, empty or white-space alt resolves to the default; a real alt is kept as written.</summary>
    [Theory]
    [InlineData(null, A2uiSurfaceCatalog.DefaultImageAlt)]
    [InlineData("", A2uiSurfaceCatalog.DefaultImageAlt)]
    [InlineData("   ", A2uiSurfaceCatalog.DefaultImageAlt)]
    [InlineData("A chart of latency", "A chart of latency")]
    public void ResolveImageAltFillsOnlyAMissingAlt(string? alt, string expected)
    {
        A2uiSurfaceCatalog.ResolveImageAlt(alt).ShouldBe(expected);
    }

    /// <summary>An image with no alt key renders with the default alt attribute, so the img is never unlabelled.</summary>
    [Fact]
    public void ImageWithoutAltRendersTheDefaultAlt()
    {
        var cut = RenderSurface($$"""{"components": [{"type": "image", "url": "{{InlinePng}}"}]}""");

        cut.Find("img.autonogrammer-a2ui-image").GetAttribute("alt").ShouldBe(A2uiSurfaceCatalog.DefaultImageAlt);
    }

    /// <summary>An image whose alt is white space renders the default alt attribute.</summary>
    [Fact]
    public void ImageWithWhiteSpaceAltRendersTheDefaultAlt()
    {
        var cut = RenderSurface($$"""{"components": [{"type": "image", "url": "{{InlinePng}}", "alt": "  "}]}""");

        cut.Find("img.autonogrammer-a2ui-image").GetAttribute("alt").ShouldBe(A2uiSurfaceCatalog.DefaultImageAlt);
    }

    /// <summary>An image whose alt the agent supplied keeps that alt.</summary>
    [Fact]
    public void ImageWithAltKeepsIt()
    {
        var cut = RenderSurface($$"""{"components": [{"type": "image", "url": "{{InlinePng}}", "alt": "Latency chart"}]}""");

        cut.Find("img.autonogrammer-a2ui-image").GetAttribute("alt").ShouldBe("Latency chart");
    }

    /// <summary>A remote image with a white-space alt renders a link named by its source, never an empty link.</summary>
    [Fact]
    public void RemoteImageWithWhiteSpaceAltIsNamedBySource()
    {
        var cut = RenderSurface("""{"components": [{"type": "image", "url": "https://example.com/i.png", "alt": "  "}]}""");

        cut.Find("a.autonogrammer-a2ui-image-link").TextContent.ShouldBe("https://example.com/i.png");
    }

    private IRenderedComponent<UiBlockRenderer> RenderSurface(string json)
    {
        using var payload = JsonDocument.Parse(json);
        return Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new A2uiSurfaceBlock { Id = "a-1", SurfaceId = "s-1", Payload = payload.RootElement.Clone() }));
    }
}
