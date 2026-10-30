using Shouldly;
using Xunit;

namespace Autonogrammer.Worktrees.Tests;

/// <summary>
/// Integration tests of <see cref="WorktreeManager"/> against real git on a temporary repository.
/// </summary>
public sealed class WorktreeManagerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TwoRunsInOneRepositoryGetDistinctPathsAndBothAppearInWorktreeList()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);

        var first = await manager.CreateAsync(repository.RepositoryPath, "run-one", Token).ConfigureAwait(true);
        var second = await manager.CreateAsync(repository.RepositoryPath, "run-two", Token).ConfigureAwait(true);

        string.Equals(first.Path, second.Path, StringComparison.Ordinal).ShouldBeFalse();
        Directory.Exists(first.Path).ShouldBeTrue();
        Directory.Exists(second.Path).ShouldBeTrue();

        var entries = await ListWorktreesAsync(repository).ConfigureAwait(true);
        entries.ShouldContain(entry => entry.Branch == "refs/heads/autonogrammer/run/run-one" && Path.GetFileName(entry.Path) == "run-one");
        entries.ShouldContain(entry => entry.Branch == "refs/heads/autonogrammer/run/run-two" && Path.GetFileName(entry.Path) == "run-two");

        var listed = await manager.ListAsync(repository.RepositoryPath, Token).ConfigureAwait(true);
        listed.Select(run => run.RunId).ShouldBe(["run-one", "run-two"], ignoreOrder: true);
    }

    [Fact]
    public async Task FileWrittenInOneWorktreeStaysOutOfTheOtherWorktreeAndTheMainCheckout()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);
        var headBefore = (await GitCli.RunAsync(repository.RepositoryPath, "rev-parse", "HEAD").ConfigureAwait(true)).Trim();

        var first = await manager.CreateAsync(repository.RepositoryPath, "run-a", Token).ConfigureAwait(true);
        var second = await manager.CreateAsync(repository.RepositoryPath, "run-b", Token).ConfigureAwait(true);

        await File.WriteAllTextAsync(Path.Combine(first.Path, "agent-output.txt"), "written by run-a\n", Token).ConfigureAwait(true);

        File.Exists(Path.Combine(first.Path, "agent-output.txt")).ShouldBeTrue();
        File.Exists(Path.Combine(second.Path, "agent-output.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(repository.RepositoryPath, "agent-output.txt")).ShouldBeFalse();

        (await GitCli.RunAsync(first.Path, "status", "--porcelain").ConfigureAwait(true)).ShouldContain("agent-output.txt");
        (await GitCli.RunAsync(second.Path, "status", "--porcelain").ConfigureAwait(true)).ShouldBe(string.Empty);
        (await GitCli.RunAsync(repository.RepositoryPath, "status", "--porcelain").ConfigureAwait(true)).ShouldBe(string.Empty);
        (await GitCli.RunAsync(repository.RepositoryPath, "rev-parse", "HEAD").ConfigureAwait(true)).Trim().ShouldBe(headBefore);
    }

    [Fact]
    public async Task ClosingARunRemovesItsDirectoryWorktreeEntryAndBranch()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);
        var run = await manager.CreateAsync(repository.RepositoryPath, "run-close", Token).ConfigureAwait(true);

        await manager.CloseAsync("run-close", Token).ConfigureAwait(true);

        Directory.Exists(run.Path).ShouldBeFalse();
        var entries = await ListWorktreesAsync(repository).ConfigureAwait(true);
        entries.ShouldNotContain(entry => Path.GetFileName(entry.Path) == "run-close");
        (await GitCli.RunAsync(repository.RepositoryPath, "for-each-ref", "--format=%(refname)", "refs/heads/").ConfigureAwait(true))
            .ShouldNotContain("refs/heads/autonogrammer/run/run-close");
        (await manager.ListAsync(repository.RepositoryPath, Token).ConfigureAwait(true)).ShouldBeEmpty();
        (await GitCli.RunAsync(repository.RepositoryPath, "status", "--porcelain").ConfigureAwait(true)).ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ARunIdCanBeCreatedAgainAfterItsRunIsClosed()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);
        await manager.CreateAsync(repository.RepositoryPath, "run-again", Token).ConfigureAwait(true);
        await manager.CloseAsync("run-again", Token).ConfigureAwait(true);

        var recreated = await manager.CreateAsync(repository.RepositoryPath, "run-again", Token).ConfigureAwait(true);

        Directory.Exists(recreated.Path).ShouldBeTrue();
        (await manager.ListAsync(repository.RepositoryPath, Token).ConfigureAwait(true)).Select(run => run.RunId).ShouldBe(["run-again"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("slash/inside")]
    [InlineData("../escape")]
    [InlineData("under_score")]
    [InlineData("dot.dot")]
    [InlineData("na\u00efve")]
    [InlineData("trailing\n")]
    public async Task InvalidRunIdIsRejectedAndCreatesNothing(string runId)
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);

        await Should.ThrowAsync<InvalidRunIdException>(() => manager.CreateAsync(repository.RepositoryPath, runId, Token)).ConfigureAwait(true);

        Directory.Exists(repository.WorktreeRoot).ShouldBeFalse();
        (await manager.ListAsync(repository.RepositoryPath, Token).ConfigureAwait(true)).ShouldBeEmpty();
        (await GitCli.RunAsync(repository.RepositoryPath, "for-each-ref", "--format=%(refname)", "refs/heads/").ConfigureAwait(true))
            .Trim().ShouldBe("refs/heads/main");
    }

    [Fact]
    public async Task NullRunIdIsRejectedAndCreatesNothing()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);

        await Should.ThrowAsync<InvalidRunIdException>(() => manager.CreateAsync(repository.RepositoryPath, null!, Token)).ConfigureAwait(true);

        Directory.Exists(repository.WorktreeRoot).ShouldBeFalse();
        (await GitCli.RunAsync(repository.RepositoryPath, "for-each-ref", "--format=%(refname)", "refs/heads/").ConfigureAwait(true))
            .Trim().ShouldBe("refs/heads/main");
    }

    [Fact]
    public async Task RunIdLengthBoundaryIsSixtyFourAcceptedAndSixtyFiveRejected()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);
        var longestId = new string('a', 64);
        var tooLongId = new string('b', 65);

        await Should.ThrowAsync<InvalidRunIdException>(() => manager.CreateAsync(repository.RepositoryPath, tooLongId, Token)).ConfigureAwait(true);
        Directory.Exists(Path.Combine(repository.WorktreeRoot, tooLongId)).ShouldBeFalse();

        var run = await manager.CreateAsync(repository.RepositoryPath, longestId, Token).ConfigureAwait(true);

        Directory.Exists(run.Path).ShouldBeTrue();
        run.Branch.ShouldBe("autonogrammer/run/" + longestId);
    }

    [Fact]
    public async Task SecondCreateWithTheSameRunIdIsRefusedAndLeavesTheFirstRunIntact()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);
        var first = await manager.CreateAsync(repository.RepositoryPath, "run-dup", Token).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(first.Path, "keep.txt"), "still here\n", Token).ConfigureAwait(true);

        await Should.ThrowAsync<RunAlreadyExistsException>(() => manager.CreateAsync(repository.RepositoryPath, "run-dup", Token)).ConfigureAwait(true);

        File.Exists(Path.Combine(first.Path, "keep.txt")).ShouldBeTrue();
        (await manager.ListAsync(repository.RepositoryPath, Token).ConfigureAwait(true)).Select(run => run.RunId).ShouldBe(["run-dup"]);
    }

    [Fact]
    public async Task ConcurrentCreatesWithDifferentRunIdsSucceedWithDistinctPaths()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);
        string[] runIds = ["run-c1", "run-c2", "run-c3", "run-c4"];

        var runs = await Task.WhenAll(runIds.Select(runId => manager.CreateAsync(repository.RepositoryPath, runId, Token))).ConfigureAwait(true);

        runs.Select(run => run.Path).Distinct(StringComparer.Ordinal).Count().ShouldBe(runIds.Length);
        runs.All(run => Directory.Exists(run.Path)).ShouldBeTrue();
        (await manager.ListAsync(repository.RepositoryPath, Token).ConfigureAwait(true)).Select(run => run.RunId)
            .ShouldBe(runIds, ignoreOrder: true);
    }

    [Fact]
    public async Task ConcurrentCreatesWithTheSameRunIdLetExactlyOneSucceed()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);

        var attempts = Enumerable.Range(0, 4)
            .Select(_ => TryCreateAsync(manager, repository.RepositoryPath, "run-race"));
        var outcomes = await Task.WhenAll(attempts).ConfigureAwait(true);

        outcomes.Count(created => created).ShouldBe(1);
        (await manager.ListAsync(repository.RepositoryPath, Token).ConfigureAwait(true)).Select(run => run.RunId).ShouldBe(["run-race"]);
    }

    [Fact]
    public async Task CloseOfAnUnknownRunIsRefusedAndChangesNothing()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);
        var kept = await manager.CreateAsync(repository.RepositoryPath, "run-kept", Token).ConfigureAwait(true);

        await Should.ThrowAsync<RunNotFoundException>(() => manager.CloseAsync("run-missing", Token)).ConfigureAwait(true);

        Directory.Exists(kept.Path).ShouldBeTrue();
        (await manager.ListAsync(repository.RepositoryPath, Token).ConfigureAwait(true)).Select(run => run.RunId).ShouldBe(["run-kept"]);
    }

    [Fact]
    public async Task GitExecutableThatCannotBeStartedIsReportedAsATypedError()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot, "autonogrammer-no-such-git-executable");

        await Should.ThrowAsync<WorktreeGitException>(() => manager.CreateAsync(repository.RepositoryPath, "run-nogit", Token)).ConfigureAwait(true);

        Directory.Exists(repository.WorktreeRoot).ShouldBeFalse();
        (await GitCli.RunAsync(repository.RepositoryPath, "for-each-ref", "--format=%(refname)", "refs/heads/").ConfigureAwait(true))
            .Trim().ShouldBe("refs/heads/main");
    }

    private static async Task<bool> TryCreateAsync(WorktreeManager manager, string repositoryPath, string runId)
    {
        try
        {
            await manager.CreateAsync(repositoryPath, runId, Token).ConfigureAwait(true);
            return true;
        }
        catch (RunAlreadyExistsException)
        {
            return false;
        }
    }

    private static async Task<IReadOnlyList<WorktreeEntry>> ListWorktreesAsync(TempRepository repository)
    {
        var porcelain = await GitCli.RunAsync(repository.RepositoryPath, "worktree", "list", "--porcelain").ConfigureAwait(true);
        return WorktreeEntry.ParsePorcelain(porcelain);
    }
}
