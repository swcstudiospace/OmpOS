using System.Text;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests;

/// <summary>
/// Verifies text and regex search over a workspace tree (ADE-18).
/// </summary>
public sealed class WorkspaceSearchTests
{
    private static readonly SearchHit[] AlphaHits =
    [
        new("docs/notes.md", 2, "alpha"),
        new("src/app.txt", 1, "alpha"),
        new("src/app.txt", 2, "alpha"),
    ];

    private static readonly SearchHit[] RegexHits =
    [
        new("log.txt", 1, "error 404"),
        new("log.txt", 3, "error 500"),
        new("log.txt", 3, "error 503"),
    ];

    private static readonly SearchHit[] SmallFileHit = [new("small.txt", 1, "needle")];
    private static readonly int[] IgnoreCaseLines = [1, 2];

    [Fact]
    public async Task TextSearchReturnsFileLineAndMatchForEachHit()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("src/app.txt", "alpha\nbeta alpha\n");
        workspace.Write("docs/notes.md", "gamma\nalpha\n");
        workspace.Write(".git/config", "alpha\n");
        var search = new WorkspaceSearch(workspace.Root);

        var result = await search.SearchAsync(
                new SearchRequest("alpha", SearchKind.Text),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.IsError.ShouldBeFalse();
        result.Truncated.ShouldBeFalse();
        result.Hits.ToArray().ShouldBe(AlphaHits);
    }

    [Fact]
    public async Task RegexSearchReturnsMatchText()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("log.txt", "error 404\nwarn 12\nerror 500 and error 503\n");
        var search = new WorkspaceSearch(workspace.Root);

        var result = await search.SearchAsync(
                new SearchRequest(@"error \d+", SearchKind.Regex),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Hits.ToArray().ShouldBe(RegexHits);
    }

    [Fact]
    public async Task TextSearchIgnoresCaseWhenRequested()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("a.txt", "Alpha\nALPHA\nbeta\n");
        var search = new WorkspaceSearch(workspace.Root);

        var result = await search.SearchAsync(
                new SearchRequest("alpha", SearchKind.Text, IgnoreCase: true),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Hits.Select(hit => hit.LineNumber).ToArray().ShouldBe(IgnoreCaseLines);
    }

    [Fact]
    public async Task InvalidRegexIsAnErrorResult()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("a.txt", "anything\n");
        var search = new WorkspaceSearch(workspace.Root);

        var result = await search.SearchAsync(
                new SearchRequest("(unclosed", SearchKind.Regex),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.IsError.ShouldBeTrue();
        result.Hits.ShouldBeEmpty();
    }

    [Fact]
    public async Task EmptyPatternIsAnErrorResult()
    {
        using var workspace = new TempWorkspace();
        var search = new WorkspaceSearch(workspace.Root);

        var result = await search.SearchAsync(
                new SearchRequest(string.Empty, SearchKind.Text),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task RegexMatchTimeoutIsAnErrorResult()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("slow.txt", new string('a', 40) + "b\n");
        var search = new WorkspaceSearch(workspace.Root);

        var result = await search.SearchAsync(
                new SearchRequest("^(a+)+$", SearchKind.Regex),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.IsError.ShouldBeTrue();
        result.Hits.ShouldBeEmpty();
    }

    [Fact]
    public async Task StopsAtMaxHitsAndReportsTruncation()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("a.txt", "x\nx\nx\n");
        var search = new WorkspaceSearch(workspace.Root);

        var limited = await search.SearchAsync(
                new SearchRequest("x", SearchKind.Text, MaxHits: 2),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        var exact = await search.SearchAsync(
                new SearchRequest("x", SearchKind.Text, MaxHits: 3),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        limited.Hits.Count.ShouldBe(2);
        limited.Truncated.ShouldBeTrue();
        exact.Hits.Count.ShouldBe(3);
        exact.Truncated.ShouldBeFalse();
    }

    [Fact]
    public async Task SkipsFilesOverSizeCapAndBinaryFiles()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("small.txt", "needle\n");
        var oversized = new byte[WorkspaceSearch.MaxFileBytes + 1];
        Encoding.ASCII.GetBytes("needle\n").CopyTo(oversized, 0);
        workspace.WriteBytes("big.txt", oversized);
        workspace.WriteBytes("binary.bin", [0x00, .. Encoding.ASCII.GetBytes("needle\n")]);
        var search = new WorkspaceSearch(workspace.Root);

        var result = await search.SearchAsync(
                new SearchRequest("needle", SearchKind.Text),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Hits.ToArray().ShouldBe(SmallFileHit);
    }

    [Fact]
    public async Task DoesNotFollowSymbolicLinksOutOfRoot()
    {
        using var workspace = new TempWorkspace();
        using var outside = new TempWorkspace();
        outside.Write("secret.txt", "needle\n");
        outside.Write("dir/secret.txt", "needle\n");
        File.CreateSymbolicLink(
            Path.Combine(workspace.RootPath, "linked.txt"),
            Path.Combine(outside.RootPath, "secret.txt"));
        File.CreateSymbolicLink(
            Path.Combine(workspace.RootPath, "linked-dir"),
            Path.Combine(outside.RootPath, "dir"));
        var search = new WorkspaceSearch(workspace.Root);

        var result = await search.SearchAsync(
                new SearchRequest("needle", SearchKind.Text),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Hits.ShouldBeEmpty();
    }
}
