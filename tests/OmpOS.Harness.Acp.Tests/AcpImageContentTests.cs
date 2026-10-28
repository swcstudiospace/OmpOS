using System.Text.Json;
using OmpOS.Harness.Acp.AcpProtocol;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>Image blocks in a prompt: the wire shape omp reads, the order and the size caps.</summary>
public sealed class AcpImageContentTests
{
    private static AcpPromptImage Img(string name, int length, string mime = "image/png") => new(name, mime, new byte[length]);

    /// <summary>An image-only message serialises to the exact image block shape: type, base64 data, mimeType.</summary>
    [Fact]
    public void ImageOnlyRequestSerialisesToExactWireForm()
    {
        var images = new[] { new AcpPromptImage("a.png", "image/png", new byte[] { 1, 2, 3 }) };
        var request = new AcpFrames.Request<AcpPromptContentParams>(4, "session/prompt", AcpPromptContent.BuildParamsWithImages("s1", string.Empty, [], images));

        var json = JsonSerializer.Serialize(request, AcpPromptContentJsonContext.Default.RequestAcpPromptContentParams);

        json.ShouldBe(
            "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"session/prompt\",\"params\":{\"sessionId\":\"s1\",\"prompt\":[{\"type\":\"image\",\"data\":\"AQID\",\"mimeType\":\"image/png\"}]}}");
    }

    /// <summary>Text keeps its place and the image blocks follow; a slash command stays first.</summary>
    [Fact]
    public void ImagesFollowTextAndASlashCommandStaysFirst()
    {
        var blocks = AcpPromptContent.BuildWithImages("/review this", [], [Img("a.png", 4)]);

        blocks.Select(b => b.Type).ShouldBe(["text", "image"]);
        blocks[0].Text.ShouldBe("/review this");
    }

    /// <summary>Images past the count, single-size or total caps, or of another type, are left out and named in a note.</summary>
    [Fact]
    public void LimitsAndTypesOmitImagesWithANote()
    {
        var limits = new AcpImageLimits(MaxImages: 2, MaxImageBytes: 10, MaxTotalBytes: 15);

        var blocks = AcpImageContent.Build([Img("ok.png", 8), Img("big.png", 11), Img("two.png", 8), Img("bad.bmp", 2, "image/bmp")], limits);

        blocks.Count(b => string.Equals(b.Type, "image", StringComparison.Ordinal)).ShouldBe(1);
        blocks[^1].Type.ShouldBe("text");
        blocks[^1].Text.ShouldNotBeNull().ShouldContain("big.png", Case.Sensitive);
        blocks[^1].Text.ShouldNotBeNull().ShouldContain("two.png", Case.Sensitive);
        blocks[^1].Text.ShouldNotBeNull().ShouldContain("bad.bmp", Case.Sensitive);
    }

    /// <summary>A message with no text, no resource and no usable image is still refused.</summary>
    [Fact]
    public void NothingToSendThrows()
    {
        Should.Throw<ArgumentException>(() => AcpPromptContent.BuildWithImages(" ", [], []));
    }
}
