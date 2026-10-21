using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.GenUi;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>Permission-option folding and A2UI image-URL allowlisting (WR-01, WR-06).</summary>
internal sealed class SurfaceSafetyTests
{
    /// <summary>A permission request without an options array still offers the non-destructive Dismiss option.</summary>
    [Fact]
    public void MissingOptionsFallBackToDismiss()
    {
        var tree = UiFolder.Fold(UiTree.Empty, PermissionEvent("{\"cmd\":\"ls\"}"));

        tree.Blocks.Single().ShouldBeOfType<PermissionBlock>().Options.ShouldBe(["Dismiss"]);
    }

    /// <summary>A permission request with a non-JSON payload still offers Dismiss instead of zero buttons.</summary>
    [Fact]
    public void NonJsonPayloadFallsBackToDismiss()
    {
        var tree = UiFolder.Fold(UiTree.Empty, PermissionEvent("not json"));

        tree.Blocks.Single().ShouldBeOfType<PermissionBlock>().Options.ShouldBe(["Dismiss"]);
    }

    /// <summary>Agent-supplied options still render verbatim, in order.</summary>
    [Fact]
    public void VerbatimOptionsArePreserved()
    {
        var tree = UiFolder.Fold(UiTree.Empty, PermissionEvent("{\"options\":[\"allow\",\"deny\"]}"));

        tree.Blocks.Single().ShouldBeOfType<PermissionBlock>().Options.ShouldBe(["allow", "deny"]);
    }

    /// <summary>Only inline raster data images render as an img; remote and relative sources fall back to a link.</summary>
    /// <param name="candidate">The candidate image source.</param>
    /// <param name="rendersImg">Whether the source renders as an img element.</param>
    [Theory]
    [InlineData("data:image/png;base64,iVBORw0KGgo=", true)]
    [InlineData("data:image/jpeg;base64,/9j/", true)]
    [InlineData("data:image/gif;base64,R0lGODlh", true)]
    [InlineData("data:image/webp;base64,UklGRg==", true)]
    [InlineData("data:image/svg+xml,<svg></svg>", false)]
    [InlineData("data:text/html,<script>alert(1)</script>", false)]
    [InlineData("https://cdn.example/img.png", false)]
    [InlineData("http://cdn.example/img.png", false)]
    [InlineData("/images/local.png", false)]
    [InlineData("//cdn.example/img.png", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///etc/passwd", false)]
    public void OnlyInlineRasterImagesRenderImg(string candidate, bool rendersImg)
    {
        // Literal JSON: reflection-based serialization is disabled for the test app, and the
        // theory candidates carry no quotes or backslashes that would need escaping.
        using var doc = JsonDocument.Parse($"{{\"components\":[{{\"type\":\"image\",\"url\":\"{candidate}\",\"alt\":\"A picture\"}}]}}");

        var component = A2uiSurfaceCatalog.GetComponents(doc.RootElement).Single();

        (component.ImageUrl is not null).ShouldBe(rendersImg);
    }

    /// <summary>A non-renderable image source keeps a fallback link with the alt text and a link-allowed href.</summary>
    [Theory]
    [InlineData("https://cdn.example/img.png", "https://cdn.example/img.png")]
    [InlineData("/images/local.png", "/images/local.png")]
    [InlineData("javascript:alert(1)", null)]
    public void NonRenderableImagesFallBackToLink(string candidate, string? expectedHref)
    {
        using var doc = JsonDocument.Parse($"{{\"components\":[{{\"type\":\"image\",\"url\":\"{candidate}\",\"alt\":\"A picture\"}}]}}");

        var component = A2uiSurfaceCatalog.GetComponents(doc.RootElement).Single();

        component.ImageUrl.ShouldBeNull();
        component.ImageSource.ShouldBe(candidate);
        component.ImageLinkText.ShouldBe("A picture");
        component.ImageLinkHref.ShouldBe(expectedHref);
    }

    /// <summary>A non-renderable image without alt text falls back to the source text.</summary>
    [Fact]
    public void ImageFallbackWithoutAltUsesSourceText()
    {
        using var doc = JsonDocument.Parse("{\"components\":[{\"type\":\"image\",\"url\":\"https://cdn.example/img.png\"}]}");

        var component = A2uiSurfaceCatalog.GetComponents(doc.RootElement).Single();

        component.ImageLinkText.ShouldBe("https://cdn.example/img.png");
    }

    private static StreamEvent PermissionEvent(string payloadJson)
    {
        var obj = new JsonObject
        {
            ["kind"] = "permission_request",
            ["callId"] = "call-9",
            ["name"] = "bash",
            ["payloadJson"] = payloadJson,
        };
        using var doc = JsonDocument.Parse(obj.ToJsonString());
        return new StreamEvent
        {
            Seq = 1,
            SessionId = "s-1",
            CorrelationId = "s-1/turn-1",
            Timestamp = new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.Zero),
            Payload = new UnknownPayload { OriginalKind = "permission_request", Raw = doc.RootElement.Clone() },
        };
    }
}
