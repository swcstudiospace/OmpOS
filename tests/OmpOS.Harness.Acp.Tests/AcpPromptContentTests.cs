using System.Text;
using System.Text.Json;
using OmpOS.Harness.Acp.AcpProtocol;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>Embedding @-mention resources in a prompt: block order, wire shape and size caps.</summary>
public sealed class AcpPromptContentTests
{
    private static AcpPromptResource Res(string name, string text, string mime = "text/plain", bool truncated = false, long original = 0) =>
        new($"file:///w/{name}", mime, text, name, original, truncated);

    /// <summary>Resources come first in order and the user text is the last block.</summary>
    [Fact]
    public void ResourcesPrecedeUserText()
    {
        var blocks = AcpPromptContent.Build("look", [Res("a.txt", "hi"), Res("b.txt", "yo")]);

        blocks.Count.ShouldBe(3);
        blocks[0].Type.ShouldBe("resource");
        blocks[0].Resource!.Location.ShouldBe("file:///w/a.txt");
        blocks[1].Resource!.Text.ShouldBe("yo");
        blocks[2].Type.ShouldBe("text");
        blocks[2].Text.ShouldBe("look");
        blocks[2].Resource.ShouldBeNull();
    }

    /// <summary>The serialised request matches the exact ACP wire form.</summary>
    [Fact]
    public void RequestSerialisesToExactWireForm()
    {
        var request = new AcpFrames.Request<AcpPromptContentParams>(7, "session/prompt", AcpPromptContent.BuildParams("s1", "look", [Res("a.txt", "hi")]));

        var json = JsonSerializer.Serialize(request, AcpPromptContentJsonContext.Default.RequestAcpPromptContentParams);

        json.ShouldBe(
            "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"session/prompt\",\"params\":{\"sessionId\":\"s1\",\"prompt\":[{\"type\":\"resource\",\"resource\":{\"uri\":\"file:///w/a.txt\",\"mimeType\":\"text/plain\",\"text\":\"hi\"}},{\"type\":\"text\",\"text\":\"look\"}]}}");
    }

    /// <summary>A leading slash command keeps its text block first.</summary>
    [Fact]
    public void SlashCommandTextComesFirst()
    {
        var blocks = AcpPromptContent.Build("  /review now", [Res("a.txt", "hi")]);

        blocks[0].Type.ShouldBe("text");
        blocks[0].Text.ShouldBe("  /review now");
        blocks[1].Type.ShouldBe("resource");
    }

    /// <summary>A multibyte cut lands on a code point boundary and carries the truncation note.</summary>
    [Fact]
    public void PerResourceCapKeepsWholeCodePoints()
    {
        var text = "\u20ac\u20ac\ud83d\ude00\u20ac";
        var full = Encoding.UTF8.GetByteCount(text);

        var blocks = AcpPromptContent.Build("t", [Res("m.txt", text)], new AcpPromptLimits(MaxResourceBytes: 8));

        var shown = blocks[0].Resource!.Text;
        shown.ShouldBe($"\u20ac\u20ac\n[truncated: showing 6 of {full} bytes]");
        shown.ShouldNotContain("\ud83d");
    }

    /// <summary>An emoji that fits exactly is kept whole.</summary>
    [Fact]
    public void EmojiFittingTheCapIsKept()
    {
        var blocks = AcpPromptContent.Build("t", [Res("m.txt", "ab\ud83d\ude00cd")], new AcpPromptLimits(MaxResourceBytes: 6));

        blocks[0].Resource!.Text.ShouldBe("ab\ud83d\ude00\n[truncated: showing 6 of 8 bytes]");
    }

    /// <summary>The total budget is spent in order and a later resource is cut, then dropped.</summary>
    [Fact]
    public void TotalCapAppliesAcrossResources()
    {
        var blocks = AcpPromptContent.Build(
            "t",
            [Res("a.txt", "aaaaaa"), Res("b.txt", "bbbbbb"), Res("c.txt", "cccccc")],
            new AcpPromptLimits(MaxResourceBytes: 100, MaxTotalBytes: 9));

        blocks[0].Resource!.Text.ShouldBe("aaaaaa");
        blocks[1].Resource!.Text.ShouldBe("bbb\n[truncated: showing 3 of 6 bytes]");
        blocks[2].Text.ShouldBe("t");
        blocks[3].Text.ShouldBe("[note: 1 attached item(s) were omitted because the prompt context limit was reached: c.txt]");
        blocks.Count.ShouldBe(4);
    }

    /// <summary>Resources beyond the count limit are dropped and named, at most five names.</summary>
    [Fact]
    public void ResourcesPastTheLimitAreNamedInANote()
    {
        var resources = Enumerable.Range(0, 9).Select(i => Res($"f{i}.txt", "x")).ToArray();

        var blocks = AcpPromptContent.Build("t", resources, new AcpPromptLimits(MaxResources: 2));

        blocks.Count(b => string.Equals(b.Type, "resource", StringComparison.Ordinal)).ShouldBe(2);
        blocks[^1].Text.ShouldBe(
            "[note: 7 attached item(s) were omitted because the prompt context limit was reached: f2.txt, f3.txt, f4.txt, f5.txt, f6.txt, and 2 more]");
    }

    /// <summary>An upstream truncation flag adds the note even when nothing is cut here.</summary>
    [Fact]
    public void UpstreamTruncatedFlagAddsNote()
    {
        var blocks = AcpPromptContent.Build("t", [Res("a.txt", "abc", truncated: true, original: 500)]);

        blocks[0].Resource!.Text.ShouldBe("abc\n[truncated: showing 3 of 500 bytes]");
    }

    /// <summary>A blank MIME type defaults to text/plain.</summary>
    [Fact]
    public void BlankMimeDefaultsToTextPlain()
    {
        var blocks = AcpPromptContent.Build("t", [Res("a.md", "x", mime: " ")]);

        blocks[0].Resource!.MimeType.ShouldBe("text/plain");
    }

    /// <summary>Bad arguments are rejected.</summary>
    [Fact]
    public void ArgumentsAreValidated()
    {
        Should.Throw<ArgumentNullException>(() => AcpPromptContent.Build(null!, []));
        Should.Throw<ArgumentNullException>(() => AcpPromptContent.Build("t", null!));
        Should.Throw<ArgumentException>(() => AcpPromptContent.Build("  ", []));
        Should.Throw<ArgumentException>(() => AcpPromptContent.Build("t", [new AcpPromptResource(" ", "text/plain", "x", "n", 1, false)]));
        AcpPromptContent.Build(string.Empty, [Res("a.txt", "x")]).Count.ShouldBe(1);
    }
}
