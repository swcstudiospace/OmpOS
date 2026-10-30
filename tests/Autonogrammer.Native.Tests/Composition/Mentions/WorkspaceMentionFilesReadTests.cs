using System.Text;
using Autonogrammer.Native.Composition.Mentions;
using Autonogrammer.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition.Mentions;

/// <summary>Reading a workspace file for a mention: the text and its size, the cut at a byte cap, and everything that must be refused.</summary>
public sealed class WorkspaceMentionFilesReadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly byte[] AccentedAndEmoji = Encoding.UTF8.GetBytes("a\u00E9\U0001F600b");

    /// <summary>A small text file is returned whole with its byte size.</summary>
    [Fact]
    public async Task ASmallTextFileIsReadWhole()
    {
        using var tree = new MentionWorkspaceTree();
        tree.Write("src/hello.txt", "h\u00E9llo\n");
        var files = new WorkspaceMentionFiles(tree.Root);

        var content = await files.ReadAsync("src/hello.txt", 1024, Token).ConfigureAwait(true);

        content.Text.ShouldBe("h\u00E9llo\n");
        content.SizeBytes.ShouldBe(7);
        content.Truncated.ShouldBeFalse();
    }

    /// <summary>An empty file reads as empty text, not as an error.</summary>
    [Fact]
    public async Task AnEmptyFileReadsAsEmptyText()
    {
        using var tree = new MentionWorkspaceTree();
        tree.Write("empty.txt", string.Empty);
        var files = new WorkspaceMentionFiles(tree.Root);

        var content = await files.ReadAsync("empty.txt", 1024, Token).ConfigureAwait(true);

        content.ShouldBe(new MentionContent(string.Empty, 0, false));
    }

    /// <summary>A leading byte order mark is not part of the text, but it counts in the size.</summary>
    [Fact]
    public async Task AByteOrderMarkIsDroppedFromTheText()
    {
        using var tree = new MentionWorkspaceTree();
        tree.WriteBytes("bom.txt", [0xEF, 0xBB, 0xBF, (byte)'x']);
        var files = new WorkspaceMentionFiles(tree.Root);

        var content = await files.ReadAsync("bom.txt", 1024, Token).ConfigureAwait(true);

        content.ShouldBe(new MentionContent("x", 4, false));
    }

    /// <summary>The cut never splits a character: a 2-byte and a 4-byte character that do not fit are dropped whole, and the full size is still reported.</summary>
    [Theory]
    [InlineData(8, "a\u00E9\U0001F600b", false)]
    [InlineData(7, "a\u00E9\U0001F600", true)]
    [InlineData(6, "a\u00E9", true)]
    [InlineData(3, "a\u00E9", true)]
    [InlineData(2, "a", true)]
    [InlineData(0, "", true)]
    public async Task TheCutFallsOnACharacterBoundary(int maxBytes, string expected, bool truncated)
    {
        using var tree = new MentionWorkspaceTree();
        tree.WriteBytes("text.txt", AccentedAndEmoji);
        var files = new WorkspaceMentionFiles(tree.Root);

        var content = await files.ReadAsync("text.txt", maxBytes, Token).ConfigureAwait(true);

        content.Text.ShouldBe(expected);
        content.SizeBytes.ShouldBe(8);
        content.Truncated.ShouldBe(truncated);
    }

    /// <summary>A file with a NUL byte near its start is binary and is refused, even when the cap is smaller than the distance to the NUL.</summary>
    [Fact]
    public async Task ABinaryFileIsRefusedWhateverTheCap()
    {
        using var tree = new MentionWorkspaceTree();
        var bytes = new byte[2000];
        Array.Fill(bytes, (byte)'a');
        bytes[100] = 0;
        tree.WriteBytes("data.bin", bytes);
        var files = new WorkspaceMentionFiles(tree.Root);

        var refused = await Should.ThrowAsync<MentionUnavailableException>(() => files.ReadAsync("data.bin", 4, Token)).ConfigureAwait(true);

        refused.Message.ShouldBe("'data.bin' is a binary file and cannot be attached.");
    }

    /// <summary>A missing file and a folder are both refused as missing.</summary>
    [Fact]
    public async Task AMissingFileAndAFolderAreRefused()
    {
        using var tree = new MentionWorkspaceTree();
        tree.Write("src/a.cs", "a");
        var files = new WorkspaceMentionFiles(tree.Root);

        var missing = await Should.ThrowAsync<MentionUnavailableException>(() => files.ReadAsync("src/none.cs", 100, Token)).ConfigureAwait(true);
        var folder = await Should.ThrowAsync<MentionUnavailableException>(() => files.ReadAsync("src", 100, Token)).ConfigureAwait(true);

        missing.Message.ShouldBe("'src/none.cs' no longer exists in the workspace.");
        folder.Message.ShouldBe("'src' no longer exists in the workspace.");
    }

    /// <summary>A path that climbs out of the root, an absolute path and an empty path are refused, even when the target file exists.</summary>
    [Fact]
    public async Task PathsOutsideTheRootAreRefused()
    {
        using var tree = new MentionWorkspaceTree();
        var outside = tree.WriteOutside("outside.txt", "secret");
        tree.Write("src/a.cs", "a");
        var files = new WorkspaceMentionFiles(tree.Root);

        foreach (var candidate in new[] { "../outside/outside.txt", "src/../../outside/outside.txt", outside, string.Empty, "." })
        {
            var refused = await Should.ThrowAsync<MentionUnavailableException>(() => files.ReadAsync(candidate, 100, Token)).ConfigureAwait(true);
            refused.Message.ShouldBe("'" + candidate + "' is outside the workspace and cannot be attached.");
        }
    }

    /// <summary>A symbolic link to a file outside the workspace, and a file behind a symbolic link to a folder outside it, are refused.</summary>
    [Fact]
    public async Task SymbolicLinkEscapesAreRefused()
    {
        using var tree = new MentionWorkspaceTree();
        var secret = tree.WriteOutside("secret.txt", "secret");
        try
        {
            File.CreateSymbolicLink(Path.Combine(tree.Root, "linked.txt"), secret);
            Directory.CreateSymbolicLink(Path.Combine(tree.Root, "linked-folder"), tree.Outside);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        var files = new WorkspaceMentionFiles(tree.Root);

        var file = await Should.ThrowAsync<MentionUnavailableException>(() => files.ReadAsync("linked.txt", 100, Token)).ConfigureAwait(true);
        var behind = await Should.ThrowAsync<MentionUnavailableException>(() => files.ReadAsync("linked-folder/secret.txt", 100, Token)).ConfigureAwait(true);

        file.Message.ShouldBe("'linked.txt' is a symbolic link and cannot be attached.");
        behind.Message.ShouldBe("'linked-folder/secret.txt' is a symbolic link and cannot be attached.");
    }

    /// <summary>The resource identifier is the absolute <c>file:</c> URI of the full path, escaped.</summary>
    [Fact]
    public void TheUriIsTheAbsoluteFileUriOfTheFullPath()
    {
        using var tree = new MentionWorkspaceTree();
        var files = new WorkspaceMentionFiles(tree.Root);

        var uri = files.ToUri("src/a b.cs");

        uri.IsAbsoluteUri.ShouldBeTrue();
        uri.IsFile.ShouldBeTrue();
        uri.LocalPath.ShouldBe(tree.Combine("src/a b.cs"));
        uri.AbsoluteUri.ShouldEndWith("/src/a%20b.cs", Case.Sensitive);
    }

    /// <summary>A path outside the workspace has no URI.</summary>
    [Fact]
    public void APathOutsideTheRootHasNoUri()
    {
        using var tree = new MentionWorkspaceTree();
        var files = new WorkspaceMentionFiles(tree.Root);

        Should.Throw<ArgumentException>(() => files.ToUri("../outside/x.txt"));
    }
}
