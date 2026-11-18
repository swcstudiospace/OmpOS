using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>When every image is left out, the omission note is never sent as the whole message.</summary>
public sealed class AcpPromptImageNoteTests
{
    private static readonly AcpPromptImage Unsupported = new("bad.bmp", "image/bmp", new byte[] { 1, 2 });

    /// <summary>A message with no text and no resource whose only image is refused has nothing to send, so it is refused as an empty message.</summary>
    [Fact]
    public void AnOmittedImageWithNoTextIsRefusedRatherThanSentAsANoteAlone()
    {
        Should.Throw<ArgumentException>(() => AcpPromptContent.BuildWithImages(" ", [], [Unsupported]));
    }

    /// <summary>With text, the omission note follows the text and no image block is sent.</summary>
    [Fact]
    public void AnOmittedImageWithTextKeepsTheNoteAfterTheText()
    {
        var blocks = AcpPromptContent.BuildWithImages("look", [], [Unsupported]);

        blocks.Select(static block => block.Type).ShouldBe(["text", "text"]);
        blocks[0].Text.ShouldBe("look");
        blocks[1].Text.ShouldNotBeNull().ShouldContain("bad.bmp", Case.Sensitive);
    }
}
