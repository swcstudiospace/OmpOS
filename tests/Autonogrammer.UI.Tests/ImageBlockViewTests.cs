using Autonogrammer.Contracts.Ui;
using Autonogrammer.UI.Components.Blocks;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>D-27: the web image block renders alt text, refuses a non-allowlisted source, and never decodes a 10 MiB payload.</summary>
public sealed class ImageBlockViewTests : BunitContext
{
    [Fact]
    public void AKeptPngRendersAnImgWithAltTextAndASizeCap()
    {
        var cut = Render(Kept("image/png", "aGVsbG8=", "chart"));

        var img = cut.Find("img.autonogrammer-image-picture");
        img.GetAttribute("alt").ShouldBe("chart");
        img.GetAttribute("src").ShouldBe("data:image/png;base64,aGVsbG8=");
        var style = img.GetAttribute("style");
        style.ShouldNotBeNull();
        style.ShouldContain("max-width:480px");
        cut.Find("[aria-label]").GetAttribute("aria-label").ShouldBe("chart");
    }

    [Fact]
    public void ANonAllowlistedSourceNeverProducesAnImg()
    {
        var cut = Render(Kept("image/svg+xml", "aGVsbG8=", "diagram"));

        cut.FindAll("img").ShouldBeEmpty();
        cut.Find(".autonogrammer-image-fallback").TextContent.ShouldContain("diagram");
    }

    [Fact]
    public void ATenMegabytePayloadRendersAPlaceholderWithAltText()
    {
        var data = new string('A', (ToolUpdateContent.MaxImageBytes / 3) * 4 + 4);
        var cut = Render(new ImageBlock
        {
            Id = "call-1-image-0",
            CallId = "call-1",
            ToolName = "screenshot",
            MimeType = "image/png",
            DataBase64 = data,
            Description = "huge chart",
            ByteLength = ToolUpdateContent.MaxImageBytes,
            Omitted = false,
        });

        cut.FindAll("img").ShouldBeEmpty();
        cut.Markup.ShouldContain("huge chart");
        cut.Markup.ShouldNotContain(data);
    }

    private static ImageBlock Kept(string mime, string data, string description) => new()
    {
        Id = "call-1-image-0",
        CallId = "call-1",
        ToolName = "screenshot",
        MimeType = mime,
        DataBase64 = data,
        Description = description,
        ByteLength = 5,
        Omitted = false,
    };

    private IRenderedComponent<UiBlockRenderer> Render(ImageBlock block) =>
        Render<UiBlockRenderer>(parameters => parameters.Add(p => p.Block, block));
}
