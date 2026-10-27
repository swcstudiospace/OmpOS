using Shouldly;
using Xunit;

namespace OmpOS.Worktrees.Tests;

/// <summary>
/// Integration tests of <see cref="WorktreeRunSet"/> against real git in temporary directories.
/// </summary>
public sealed class WorktreeRunSetTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<string> BranchListAsync(string repository, string pattern) =>
        (await GitCli.RunAsync(repository, "branch", "--list", pattern).ConfigureAwait(true)).Trim();

    [Fact]
    public async Task EveryGitRootGetsItsOwnWorktreeAndBranchAndThePrimaryComesFirst()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var api = await set.AddRepositoryAsync("api").ConfigureAwait(true);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));

        var result = await runSet.CreateAsync([new WorktreeRootSpec("web", web), new WorktreeRootSpec("api", api)], "run1", Token).ConfigureAwait(true);

        result.IsComplete.ShouldBeTrue();
        result.RunId.ShouldBe("run1");
        result.Roots.Select(root => root.Status).ShouldBe([WorktreeRootStatus.Created, WorktreeRootStatus.Created]);
        result.PrimaryWorkingPath.ShouldBe(Path.Combine(set.WorktreeRoot, "run1-web"));
        result.AdditionalWorkingPaths.ShouldBe([Path.Combine(set.WorktreeRoot, "run1-api")]);
        result.Warnings.ShouldBeEmpty();
        Directory.Exists(result.PrimaryWorkingPath).ShouldBeTrue();
        Directory.Exists(result.AdditionalWorkingPaths[0]).ShouldBeTrue();
        (await BranchListAsync(web, "ompos/run/run1-web").ConfigureAwait(true)).ShouldContain("ompos/run/run1-web", Case.Sensitive);
        (await BranchListAsync(api, "ompos/run/run1-api").ConfigureAwait(true)).ShouldContain("ompos/run/run1-api", Case.Sensitive);
        (await BranchListAsync(web, "ompos/run/run1-api").ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task AFileWrittenInOneRootWorktreeStaysOutOfTheOtherWorktreeAndBothMainCheckouts()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var api = await set.AddRepositoryAsync("api").ConfigureAwait(true);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));
        var result = await runSet.CreateAsync([new WorktreeRootSpec("web", web), new WorktreeRootSpec("api", api)], "run1", Token).ConfigureAwait(true);

        await File.WriteAllTextAsync(Path.Combine(result.PrimaryWorkingPath, "agent.txt"), "edit\n", Token).ConfigureAwait(true);

        File.Exists(Path.Combine(result.AdditionalWorkingPaths[0], "agent.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(web, "agent.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(api, "agent.txt")).ShouldBeFalse();
        (await GitCli.RunAsync(web, "status", "--porcelain").ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ClosingTheSetRemovesEveryWorktreeAndBranchAndCanBeRepeated()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var api = await set.AddRepositoryAsync("api").ConfigureAwait(true);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));
        var result = await runSet.CreateAsync([new WorktreeRootSpec("web", web), new WorktreeRootSpec("api", api)], "run1", Token).ConfigureAwait(true);

        await runSet.CloseAsync(result, Token).ConfigureAwait(true);
        await runSet.CloseAsync(result, Token).ConfigureAwait(true);

        Directory.Exists(result.PrimaryWorkingPath).ShouldBeFalse();
        Directory.Exists(result.AdditionalWorkingPaths[0]).ShouldBeFalse();
        (await BranchListAsync(web, "ompos/run/*").ConfigureAwait(true)).ShouldBeEmpty();
        (await BranchListAsync(api, "ompos/run/*").ConfigureAwait(true)).ShouldBeEmpty();
        Directory.Exists(web).ShouldBeTrue();
        Directory.Exists(api).ShouldBeTrue();
    }

    [Fact]
    public async Task ANonGitRootIsUsedInPlaceWithAWarningAndClosingNeverTouchesIt()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var notes = set.AddPlainFolder("notes");
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));

        var result = await runSet.CreateAsync([new WorktreeRootSpec("web", web), new WorktreeRootSpec("notes", notes)], "run1", Token).ConfigureAwait(true);

        result.IsComplete.ShouldBeTrue();
        var inPlace = result.Roots[1];
        inPlace.Status.ShouldBe(WorktreeRootStatus.UsedInPlace);
        inPlace.WorkingPath.ShouldBe(notes);
        inPlace.Run.ShouldBeNull();
        result.AdditionalWorkingPaths.ShouldBe([notes]);
        result.Warnings.ShouldHaveSingleItem().ShouldContain("'notes'", Case.Sensitive);
        Directory.GetDirectories(set.WorktreeRoot).Length.ShouldBe(1);

        await runSet.CloseAsync(result, Token).ConfigureAwait(true);

        File.Exists(Path.Combine(notes, "notes.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task ASubfolderOfARepositoryIsUsedInPlaceBecauseItIsNotTheTopOfTheRepository()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var packages = Path.Combine(web, "packages");
        Directory.CreateDirectory(packages);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));

        var result = await runSet.CreateAsync([new WorktreeRootSpec("packages", packages)], "run1", Token).ConfigureAwait(true);

        result.Roots.ShouldHaveSingleItem().Status.ShouldBe(WorktreeRootStatus.UsedInPlace);
        Directory.Exists(set.WorktreeRoot).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARootInsideAnotherIsRefusedBeforeAnythingIsCreated(bool innerFirst)
    {
        using var set = new TempRepositorySet();
        var outer = await set.AddRepositoryAsync("mono").ConfigureAwait(true);
        var inner = await set.AddRepositoryAsync(Path.Combine("mono", "vendor", "lib")).ConfigureAwait(true);
        WorktreeRootSpec[] roots = innerFirst
            ? [new WorktreeRootSpec("lib", inner), new WorktreeRootSpec("mono", outer)]
            : [new WorktreeRootSpec("mono", outer), new WorktreeRootSpec("lib", inner)];
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));

        var exception = await Should.ThrowAsync<NestedWorktreeRootException>(() => runSet.CreateAsync(roots, "run1", Token)).ConfigureAwait(true);

        exception.Message.ShouldContain("inside", Case.Sensitive);
        Directory.Exists(set.WorktreeRoot).ShouldBeFalse();
    }

    [Fact]
    public async Task TheSameFolderTwiceEvenWithATrailingSeparatorIsRefused()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));

        var exception = await Should.ThrowAsync<NestedWorktreeRootException>(
            () => runSet.CreateAsync([new WorktreeRootSpec("a", web), new WorktreeRootSpec("b", web + Path.DirectorySeparatorChar)], "run1", Token)).ConfigureAwait(true);

        exception.Message.ShouldContain("same folder", Case.Sensitive);
    }

    [Fact]
    public async Task ASiblingWhoseNameStartsWithAnotherRootNameIsNotNested()
    {
        using var set = new TempRepositorySet();
        var app = await set.AddRepositoryAsync("app").ConfigureAwait(true);
        var admin = await set.AddRepositoryAsync("app-admin").ConfigureAwait(true);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));

        var result = await runSet.CreateAsync([new WorktreeRootSpec("app", app), new WorktreeRootSpec("app-admin", admin)], "run1", Token).ConfigureAwait(true);

        result.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task TheCapCountsExistingWorktreesAndRefusesASetThatWouldExceedItWithoutCreatingAnything()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var api = await set.AddRepositoryAsync("api").ConfigureAwait(true);
        var manager = new WorktreeManager(set.WorktreeRoot);
        await manager.CreateAsync(web, "old-1", Token).ConfigureAwait(true);
        await manager.CreateAsync(web, "old-2", Token).ConfigureAwait(true);
        var runSet = new WorktreeRunSet(manager, maxWorktrees: 3);

        var exception = await Should.ThrowAsync<WorktreeCapExceededException>(
            () => runSet.CreateAsync([new WorktreeRootSpec("web", web), new WorktreeRootSpec("api", api)], "run1", Token)).ConfigureAwait(true);

        exception.Cap.ShouldBe(3);
        exception.Existing.ShouldBe(2);
        exception.Requested.ShouldBe(2);
        Directory.GetDirectories(set.WorktreeRoot).Length.ShouldBe(2);
        (await BranchListAsync(api, "ompos/run/*").ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ASetThatExactlyFillsTheCapIsAllowedAndNonGitRootsDoNotCount()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var api = await set.AddRepositoryAsync("api").ConfigureAwait(true);
        var notes = set.AddPlainFolder("notes");
        var manager = new WorktreeManager(set.WorktreeRoot);
        await manager.CreateAsync(web, "old-1", Token).ConfigureAwait(true);
        var runSet = new WorktreeRunSet(manager, maxWorktrees: 3);

        var result = await runSet.CreateAsync(
            [new WorktreeRootSpec("web", web), new WorktreeRootSpec("api", api), new WorktreeRootSpec("notes", notes)],
            "run1",
            Token).ConfigureAwait(true);

        result.IsComplete.ShouldBeTrue();
        runSet.MaxWorktrees.ShouldBe(3);
    }

    [Fact]
    public async Task AFailingRootRollsBackTheWorktreesAlreadyMadeAndReportsEveryRoot()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var api = await set.AddRepositoryAsync("api").ConfigureAwait(true);
        var docs = await set.AddRepositoryAsync("docs").ConfigureAwait(true);
        await GitCli.RunAsync(api, "branch", "ompos/run/run1-api").ConfigureAwait(true);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));

        var result = await runSet.CreateAsync(
            [new WorktreeRootSpec("web", web), new WorktreeRootSpec("api", api), new WorktreeRootSpec("docs", docs)],
            "run1",
            Token).ConfigureAwait(true);

        result.IsComplete.ShouldBeFalse();
        result.Roots.Select(root => root.Status).ShouldBe([WorktreeRootStatus.RolledBack, WorktreeRootStatus.Failed, WorktreeRootStatus.NotAttempted]);
        result.Roots[1].Detail.ShouldNotBeNull().ShouldContain("'api'", Case.Sensitive);
        result.Roots[2].Detail.ShouldNotBeNull().ShouldContain("'api'", Case.Sensitive);
        Directory.GetDirectories(set.WorktreeRoot).ShouldBeEmpty();
        (await BranchListAsync(web, "ompos/run/*").ConfigureAwait(true)).ShouldBeEmpty();
        (await BranchListAsync(docs, "ompos/run/*").ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RootNamesAreReducedToSafeSegmentsAndMadeUniqueInTheWorktreeIds()
    {
        using var set = new TempRepositorySet();
        var one = await set.AddRepositoryAsync("one").ConfigureAwait(true);
        var two = await set.AddRepositoryAsync("two").ConfigureAwait(true);
        var three = await set.AddRepositoryAsync("three").ConfigureAwait(true);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));

        var result = await runSet.CreateAsync(
            [new WorktreeRootSpec("My Service / API v2!", one), new WorktreeRootSpec("my-service-api-v2", two), new WorktreeRootSpec("***", three)],
            "r",
            Token).ConfigureAwait(true);

        result.Roots.Select(root => Path.GetFileName(root.WorkingPath)).ShouldBe(["r-My-Service-API-v2", "r-my-service-api-v2-2", "r-root"]);
    }

    [Fact]
    public async Task ARunIdThatLeavesNoRoomForARootNameIsRejected()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));

        await Should.ThrowAsync<ArgumentException>(() => runSet.CreateAsync([new WorktreeRootSpec("web", web)], new string('a', 64), Token)).ConfigureAwait(true);
        Directory.Exists(set.WorktreeRoot).ShouldBeFalse();
    }

    [Fact]
    public async Task AWorktreeIdIsCutToFitSixtyFourCharacters()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));
        var runId = new string('a', 40);

        var result = await runSet.CreateAsync([new WorktreeRootSpec(new string('b', 50), web)], runId, Token).ConfigureAwait(true);

        Path.GetFileName(result.PrimaryWorkingPath).ShouldBe(runId + "-" + new string('b', 23));
    }

    [Fact]
    public async Task InvalidInputIsRejectedBeforeAnythingIsCreated()
    {
        using var set = new TempRepositorySet();
        var web = await set.AddRepositoryAsync("web").ConfigureAwait(true);
        var runSet = new WorktreeRunSet(new WorktreeManager(set.WorktreeRoot));
        var missing = Path.Combine(set.RootPath, "missing");

        await Should.ThrowAsync<ArgumentException>(() => runSet.CreateAsync([], "run1", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(() => runSet.CreateAsync([new WorktreeRootSpec("a", web), new WorktreeRootSpec("A ", web + "x")], "run1", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(() => runSet.CreateAsync([new WorktreeRootSpec("a", "relative/web")], "run1", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<DirectoryNotFoundException>(() => runSet.CreateAsync([new WorktreeRootSpec("a", missing)], "run1", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidRunIdException>(() => runSet.CreateAsync([new WorktreeRootSpec("a", web)], "bad id!", Token)).ConfigureAwait(true);

        Directory.Exists(set.WorktreeRoot).ShouldBeFalse();
    }

    [Fact]
    public void TheDefaultCapIsTwentyFiveAndAnEmptyCapIsRejected()
    {
        var manager = new WorktreeManager(Path.Combine(Path.GetTempPath(), "ompos-unused"));

        new WorktreeRunSet(manager).MaxWorktrees.ShouldBe(25);
        Should.Throw<ArgumentOutOfRangeException>(() => new WorktreeRunSet(manager, maxWorktrees: 0));
    }
}
