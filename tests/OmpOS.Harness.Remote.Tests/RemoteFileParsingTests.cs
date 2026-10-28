using System.Text;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The pure readers of the remote file scripts: a signed read behind a login banner, strict text decoding, directory listings and stat lines.</summary>
public sealed class RemoteFileParsingTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>The signature line is found after a login banner, and the content that follows it is returned untouched, even when the content mentions the marker.</summary>
    [Fact]
    public void ASignedReadIsFoundBehindABanner()
    {
        var output = Bytes("Welcome\nOMPOS_SIG 1700000000 42\nline one\nOMPOS_SIG not-a-signature\n");

        RemoteFileParsing.TrySplitSignedRead(output, out var signature, out var content).ShouldBeTrue();

        signature.ShouldBe("1700000000 42");
        Encoding.UTF8.GetString(content.Span).ShouldBe("line one\nOMPOS_SIG not-a-signature\n");
    }

    /// <summary>A marker in the middle of a line, a missing marker and a marker without a line end are not signatures.</summary>
    [Fact]
    public void ANonSignatureIsRefused()
    {
        RemoteFileParsing.TrySplitSignedRead(Bytes("xx OMPOS_SIG 1 2\n"), out _, out _).ShouldBeFalse();
        RemoteFileParsing.TrySplitSignedRead(Bytes("nothing here"), out _, out _).ShouldBeFalse();
        RemoteFileParsing.TrySplitSignedRead(Bytes("OMPOS_SIG 1 2"), out _, out _).ShouldBeFalse();
        RemoteFileParsing.TrySplitSignedRead(ReadOnlyMemory<byte>.Empty, out _, out _).ShouldBeFalse();
    }

    /// <summary>The marker with an empty signature (a remote machine without stat) still splits, with an empty signature.</summary>
    [Fact]
    public void AnEmptySignatureIsAllowed()
    {
        RemoteFileParsing.TrySplitSignedRead(Bytes("OMPOS_SIG \nbody"), out var signature, out var content).ShouldBeTrue();

        signature.ShouldBeEmpty();
        Encoding.UTF8.GetString(content.Span).ShouldBe("body");
    }

    /// <summary>Text decoding drops one byte order mark, refuses NUL in the first 8000 bytes and refuses invalid UTF-8, but accepts a NUL after the sniffed prefix.</summary>
    [Fact]
    public void DecodingIsStrict()
    {
        RemoteFileParsing.DecodeText([0xEF, 0xBB, 0xBF, 0x68, 0x69], out var hasBom).ShouldBe("hi");
        hasBom.ShouldBeTrue();
        RemoteFileParsing.DecodeText(Bytes("plain"), out hasBom).ShouldBe("plain");
        hasBom.ShouldBeFalse();
        RemoteFileParsing.DecodeText(ReadOnlySpan<byte>.Empty, out _).ShouldBeEmpty();
        Should.Throw<InvalidDataException>(() => RemoteFileParsing.DecodeText(new byte[] { 0x41, 0x00 }, out _));
        Should.Throw<InvalidDataException>(() => RemoteFileParsing.DecodeText(new byte[] { 0xC3, 0x28 }, out _));
        var late = new byte[RemoteFileParsing.BinarySniffBytes + 5];
        Array.Fill(late, (byte)'a');
        late[^1] = 0;
        RemoteFileParsing.DecodeText(late, out _).Length.ShouldBe(late.Length);
    }

    /// <summary>A listing keeps names with spaces, tabs and leading dots, marks links, notes the cut, and ignores banner lines and names with a replacement character.</summary>
    [Fact]
    public void AListingIsParsed()
    {
        var output = "Last login: today\nDL sub dir\nF. a\tb.txt\nFL link\nD. .git\nF. bad\uFFFDname\nDebian GNU/Linux\nT\n";

        var (entries, truncated) = RemoteFileParsing.ParseList(output);

        truncated.ShouldBeTrue();
        entries.ShouldBe(
        [
            ("sub dir", true, true),
            ("a\tb.txt", false, false),
            ("link", false, true),
            (".git", true, false),
        ]);
    }

    /// <summary>A stat line gives kind, size and time; a line for a missing path has only the kind; no line is no answer.</summary>
    [Fact]
    public void AStatLineIsParsed()
    {
        RemoteFileParsing.TryParseStat("banner\nOMPOS_STAT kind=file size=12 mtime=1700000000\n", out var kind, out var length, out var modified).ShouldBeTrue();
        kind.ShouldBe("file");
        length.ShouldBe(12L);
        modified.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        RemoteFileParsing.TryParseStat("OMPOS_STAT kind=missing\n", out kind, out length, out modified).ShouldBeTrue();
        kind.ShouldBe("missing");
        length.ShouldBeNull();
        modified.ShouldBeNull();

        RemoteFileParsing.TryParseStat("OMPOS_STAT kind=dir size= mtime=\n", out kind, out length, out modified).ShouldBeTrue();
        length.ShouldBeNull();
        RemoteFileParsing.TryParseStat("nothing", out _, out _, out _).ShouldBeFalse();
    }
}
