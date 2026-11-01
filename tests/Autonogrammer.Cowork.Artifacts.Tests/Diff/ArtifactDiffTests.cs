using System.Text;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Diff;
using Autonogrammer.Cowork.Artifacts.Model;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Diff;

/// <summary>Caps, cancellation and binary metadata.</summary>
public sealed class ArtifactDiffTests
{
    [Fact]
    public void OverCapDiffReturnsCountsAndHonoursCancellation()
    {
        var differ = new ArtifactDiffer();
        var left = Snapshot(ArtifactKind.Markdown, new byte[ArtifactDiffer.MaxBytes + 1], "a");
        var right = Snapshot(ArtifactKind.Markdown, "b"u8.ToArray(), "b");
        var counts = differ.Diff(left, right, TestContext.Current.CancellationToken);
        counts.CountsOnly.ShouldBeTrue();
        counts.LeftByteCount.ShouldBe(ArtifactDiffer.MaxBytes + 1);
        counts.Inserted.ShouldBe(0);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Should.Throw<OperationCanceledException>(() => differ.Diff(left, right, cancelled.Token));
    }

    [Fact]
    public void BinaryKindsDiffAsMetadata()
    {
        var differ = new ArtifactDiffer();
        var left = Snapshot(ArtifactKind.Image, [1, 2, 3], "abc");
        var right = Snapshot(ArtifactKind.Image, [4, 5], "def");
        var diff = differ.Diff(left, right, TestContext.Current.CancellationToken);
        diff.MetadataOnly.ShouldBeTrue();
        diff.LeftByteCount.ShouldBe(3);
        diff.RightByteCount.ShouldBe(2);
        diff.LeftContentSha256.ShouldBe("abc");
        diff.RightContentSha256.ShouldBe("def");
    }

    [Fact]
    public void ASmallTextDiffUsesDiffPlex()
    {
        var differ = new ArtifactDiffer();
        var left = Snapshot(ArtifactKind.Code, Encoding.UTF8.GetBytes("one\n"), "a");
        var right = Snapshot(ArtifactKind.Code, Encoding.UTF8.GetBytes("one\ntwo\n"), "b");
        var diff = differ.Diff(left, right, TestContext.Current.CancellationToken);
        diff.CountsOnly.ShouldBeFalse();
        diff.Inserted.ShouldBeGreaterThan(0);
    }

    private static ArtifactSnapshot Snapshot(ArtifactKind kind, byte[] content, string hash) => new()
    {
        ArtifactId = "id",
        Kind = kind,
        Title = "t",
        MediaType = "text/plain",
        Number = 1,
        Content = content,
        ContentSha256 = hash,
        Author = ArtifactAuthor.Agent,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };
}
