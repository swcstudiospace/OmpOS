using OmpOS.Workspace.Sets;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests.Sets;

/// <summary>
/// Verifies the recent-set store: order, replacement, the cap, persistence across instances, and a damaged file reading as empty.
/// </summary>
public sealed class WorkspaceSetStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ompos-sets-" + Guid.NewGuid().ToString("N"));

    private string StorePath => Path.Combine(_directory, "config", "workspace-sets.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static WorkspaceSet Set(string name, params string[] paths) => WorkspaceSet.FromPaths(name, paths);

    [Fact]
    public async Task ARecordedSetSurvivesANewStoreWithItsPrimaryAndNames()
    {
        var ct = TestContext.Current.CancellationToken;
        var set = Set("Team", "/work/web", "/work/api").WithPrimary("api").AddFolder("/work/docs", "Docs");
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero));
        using (var writer = new WorkspaceSetStore(StorePath, clock))
        {
            await writer.RecordAsync(set, ct).ConfigureAwait(true);
        }

        using var reader = new WorkspaceSetStore(StorePath);
        var listed = await reader.ListAsync(ct).ConfigureAwait(true);

        var recent = listed.ShouldHaveSingleItem();
        recent.LastUsedAt.ShouldBe(new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero));
        recent.Set.Id.ShouldBe("team");
        recent.Set.Name.ShouldBe("Team");
        recent.Set.Roots.Select(root => root.Path).ShouldBe(["/work/api", "/work/web", "/work/docs"]);
        recent.Set.Roots.Select(root => root.DisplayName).ShouldBe(["api", "web", "Docs"]);
        recent.Set.Primary.Id.ShouldBe("api");
    }

    [Fact]
    public async Task RecordingTheSameSetAgainReplacesItAndMovesItToTheFront()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = new WorkspaceSetStore(StorePath);
        await store.RecordAsync(Set("one", "/work/a"), ct).ConfigureAwait(true);
        await store.RecordAsync(Set("two", "/work/b"), ct).ConfigureAwait(true);

        await store.RecordAsync(Set("one", "/work/a", "/work/c"), ct).ConfigureAwait(true);
        var listed = await store.ListAsync(ct).ConfigureAwait(true);

        listed.Select(recent => recent.Set.Id).ShouldBe(["one", "two"]);
        listed[0].Set.Roots.Count.ShouldBe(2);
    }

    [Fact]
    public async Task OnlyTheMostRecentTwentySetsAreKept()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = new WorkspaceSetStore(StorePath);
        for (var index = 0; index < WorkspaceSetStore.MaxRecent + 3; index++)
        {
            await store.RecordAsync(Set($"set{index}", $"/work/r{index}"), ct).ConfigureAwait(true);
        }

        var listed = await store.ListAsync(ct).ConfigureAwait(true);

        listed.Count.ShouldBe(WorkspaceSetStore.MaxRecent);
        listed[0].Set.Id.ShouldBe($"set{WorkspaceSetStore.MaxRecent + 2}");
        listed.ShouldNotContain(recent => string.Equals(recent.Set.Id, "set0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ForgettingASetRemovesItAndReportsWhetherItExisted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = new WorkspaceSetStore(StorePath);
        await store.RecordAsync(Set("one", "/work/a"), ct).ConfigureAwait(true);

        (await store.ForgetAsync("one", ct).ConfigureAwait(true)).ShouldBeTrue();
        (await store.ForgetAsync("one", ct).ConfigureAwait(true)).ShouldBeFalse();

        (await store.ListAsync(ct).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{ "version": 99, "sets": [] }""")]
    [InlineData("""{ "version": 1, "sets": [], "surprise": true }""")]
    [InlineData("")]
    public async Task ADamagedOrForeignFileReadsAsEmptyAndIsMovedAside(string content)
    {
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        await File.WriteAllTextAsync(StorePath, content, ct).ConfigureAwait(true);
        using var store = new WorkspaceSetStore(StorePath);

        (await store.ListAsync(ct).ConfigureAwait(true)).ShouldBeEmpty();

        File.Exists(StorePath).ShouldBeFalse();
        Directory.GetFiles(Path.GetDirectoryName(StorePath)!, "workspace-sets.json.quarantined-*").Length.ShouldBe(1);
    }

    [Fact]
    public async Task AnEntryWithNoRootsIsLeftOutWhileTheOthersStay()
    {
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        const string content = """
            { "version": 1, "sets": [
              { "id": "bad", "name": "Bad", "lastUsedAt": "2026-10-27T08:00:00+00:00", "roots": [] },
              { "id": "ok", "name": "Ok", "lastUsedAt": "2026-10-27T08:00:00+00:00",
                "roots": [ { "id": "a", "path": "/work/a", "displayName": "a" } ] }
            ] }
            """;
        await File.WriteAllTextAsync(StorePath, content, ct).ConfigureAwait(true);
        using var store = new WorkspaceSetStore(StorePath);

        var listed = await store.ListAsync(ct).ConfigureAwait(true);

        listed.Select(recent => recent.Set.Id).ShouldBe(["ok"]);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
