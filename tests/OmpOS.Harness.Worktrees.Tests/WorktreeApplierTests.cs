using Shouldly;
using Xunit;

namespace OmpOS.Worktrees.Tests;

/// <summary>Integration tests of <see cref="WorktreeApplier"/> against real git in temporary directories: patch, apply with conflict reporting, list and delete.</summary>
public sealed class WorktreeApplierTests
{
    private const string Ten = "l1\nl2\nl3\nl4\nl5\nl6\nl7\nl8\nl9\nl10\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task CommitAllAsync(string path, string message)
    {
        await GitCli.RunAsync(path, "add", "-A").ConfigureAwait(true);
        await GitCli.RunAsync(path, "-c", "user.name=OmpOS Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", message).ConfigureAwait(true);
    }

    private static async Task<(WorktreeApplier Applier, WorktreeRun Run)> StartAsync(TempRepository repository, string runId = "run-a")
    {
        await File.WriteAllTextAsync(Path.Combine(repository.RepositoryPath, "notes.txt"), Ten, Token).ConfigureAwait(true);
        await CommitAllAsync(repository.RepositoryPath, "notes").ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);
        var run = await manager.CreateAsync(repository.RepositoryPath, runId, Token).ConfigureAwait(true);
        return (new WorktreeApplier(manager), run);
    }

    [Fact]
    public async Task ThePatchHoldsCommittedAndUncommittedChangesAndNewFilesWithCounts()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var (applier, run) = await StartAsync(repository).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(run.Path, "notes.txt"), Ten.Replace("l5", "L5", StringComparison.Ordinal), Token).ConfigureAwait(true);
        await CommitAllAsync(run.Path, "committed by the run").ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(run.Path, "added.txt"), "one\ntwo\n", Token).ConfigureAwait(true);
        File.Delete(Path.Combine(run.Path, "README.md"));

        var patch = await applier.CreatePatchAsync("run-a", repository.RepositoryPath, Token).ConfigureAwait(true);

        patch.IsEmpty.ShouldBeFalse();
        patch.Branch.ShouldBe("ompos/run/run-a");
        patch.Files.Select(file => (file.Path, file.Status)).ShouldBe(
            [("README.md", WorktreeFileStatus.Deleted), ("added.txt", WorktreeFileStatus.Added), ("notes.txt", WorktreeFileStatus.Modified)]);
        patch.Files.Single(file => string.Equals(file.Path, "added.txt", StringComparison.Ordinal)).Added.ShouldBe(2);
        patch.Files.Single(file => string.Equals(file.Path, "notes.txt", StringComparison.Ordinal)).Removed.ShouldBe(1);
        patch.Added.ShouldBe(3);
        patch.Text.ShouldContain("+++ b/added.txt", Case.Sensitive);
        patch.IsTextTruncated.ShouldBeFalse();
        Directory.EnumerateFiles(Path.GetTempPath(), "ompos-worktree-*.patch").Any(file => File.GetLastWriteTimeUtc(file) > DateTime.UtcNow.AddMinutes(-1)).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyingLeavesTheChangesInTheMainWorkingTreeUncommittedAndKeepsNonUtf8BytesAndBinaryFiles()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var (applier, run) = await StartAsync(repository).ConfigureAwait(true);
        byte[] latin1 = [0x63, 0x61, 0x66, 0xE9, 0x0A];
        byte[] binary = [0x00, 0x01, 0x02, 0xFF, 0x00];
        await File.WriteAllBytesAsync(Path.Combine(run.Path, "latin1.txt"), latin1, Token).ConfigureAwait(true);
        await File.WriteAllBytesAsync(Path.Combine(run.Path, "data.bin"), binary, Token).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(run.Path, "notes.txt"), Ten.Replace("l2", "L2", StringComparison.Ordinal), Token).ConfigureAwait(true);
        var headBefore = (await GitCli.RunAsync(repository.RepositoryPath, "rev-parse", "HEAD").ConfigureAwait(true)).Trim();

        var result = await applier.ApplyAsync("run-a", repository.RepositoryPath, WorktreeApplyMode.Clean, Token).ConfigureAwait(true);

        result.Status.ShouldBe(WorktreeApplyStatus.Applied);
        result.Files.Count.ShouldBe(3);
        result.Files.Single(file => string.Equals(file.Path, "data.bin", StringComparison.Ordinal)).IsBinary.ShouldBeTrue();
        (await File.ReadAllBytesAsync(Path.Combine(repository.RepositoryPath, "latin1.txt"), Token).ConfigureAwait(true)).ShouldBe(latin1);
        (await File.ReadAllBytesAsync(Path.Combine(repository.RepositoryPath, "data.bin"), Token).ConfigureAwait(true)).ShouldBe(binary);
        (await File.ReadAllTextAsync(Path.Combine(repository.RepositoryPath, "notes.txt"), Token).ConfigureAwait(true)).ShouldContain("L2", Case.Sensitive);
        (await GitCli.RunAsync(repository.RepositoryPath, "rev-parse", "HEAD").ConfigureAwait(true)).Trim().ShouldBe(headBefore);
        (await GitCli.RunAsync(repository.RepositoryPath, "diff", "--cached", "--name-only").ConfigureAwait(true)).ShouldBeEmpty();
        (await GitCli.RunAsync(repository.RepositoryPath, "status", "--porcelain").ConfigureAwait(true)).ShouldContain("notes.txt", Case.Sensitive);
    }

    [Fact]
    public async Task APatchThatDoesNotFitIsReportedPerFileAndWritesNothingInCleanMode()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var (applier, run) = await StartAsync(repository).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(run.Path, "notes.txt"), Ten.Replace("l5", "run five", StringComparison.Ordinal), Token).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(run.Path, "fresh.txt"), "fresh\n", Token).ConfigureAwait(true);
        var mainText = Ten.Replace("l5", "main five", StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(repository.RepositoryPath, "notes.txt"), mainText, Token).ConfigureAwait(true);
        await CommitAllAsync(repository.RepositoryPath, "main moved on").ConfigureAwait(true);

        var result = await applier.ApplyAsync("run-a", repository.RepositoryPath, WorktreeApplyMode.Clean, Token).ConfigureAwait(true);

        result.Status.ShouldBe(WorktreeApplyStatus.Conflicts);
        result.ConflictPaths.ShouldBe(["notes.txt"]);
        result.Message.ShouldContain("notes.txt", Case.Sensitive);
        result.Message.ShouldContain("Nothing was changed", Case.Sensitive);
        File.Exists(Path.Combine(repository.RepositoryPath, "fresh.txt")).ShouldBeFalse();
        (await File.ReadAllTextAsync(Path.Combine(repository.RepositoryPath, "notes.txt"), Token).ConfigureAwait(true)).ShouldBe(mainText);
        (await GitCli.RunAsync(repository.RepositoryPath, "status", "--porcelain").ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ThreeWayModeMergesChangesThatDoNotOverlapAndStagesThem()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var (applier, run) = await StartAsync(repository).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(run.Path, "notes.txt"), Ten.Replace("l5", "run five", StringComparison.Ordinal), Token).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(repository.RepositoryPath, "notes.txt"), Ten.Replace("l7", "main seven", StringComparison.Ordinal), Token).ConfigureAwait(true);
        await CommitAllAsync(repository.RepositoryPath, "main moved on").ConfigureAwait(true);

        var clean = await applier.ApplyAsync("run-a", repository.RepositoryPath, WorktreeApplyMode.Clean, Token).ConfigureAwait(true);
        var merged = await applier.ApplyAsync("run-a", repository.RepositoryPath, WorktreeApplyMode.ThreeWay, Token).ConfigureAwait(true);

        clean.Status.ShouldBe(WorktreeApplyStatus.Conflicts);
        merged.Status.ShouldBe(WorktreeApplyStatus.Applied);
        var text = await File.ReadAllTextAsync(Path.Combine(repository.RepositoryPath, "notes.txt"), Token).ConfigureAwait(true);
        text.ShouldContain("run five", Case.Sensitive);
        text.ShouldContain("main seven", Case.Sensitive);
        (await GitCli.RunAsync(repository.RepositoryPath, "diff", "--cached", "--name-only").ConfigureAwait(true)).ShouldContain("notes.txt", Case.Sensitive);
    }

    [Fact]
    public async Task ThreeWayModeLeavesConflictMarkersAndNamesTheConflictingFiles()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var (applier, run) = await StartAsync(repository).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(run.Path, "notes.txt"), Ten.Replace("l5", "run five", StringComparison.Ordinal), Token).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(repository.RepositoryPath, "notes.txt"), Ten.Replace("l5", "main five", StringComparison.Ordinal), Token).ConfigureAwait(true);
        await CommitAllAsync(repository.RepositoryPath, "main moved on").ConfigureAwait(true);

        var result = await applier.ApplyAsync("run-a", repository.RepositoryPath, WorktreeApplyMode.ThreeWay, Token).ConfigureAwait(true);

        result.Status.ShouldBe(WorktreeApplyStatus.Conflicts);
        result.ConflictPaths.ShouldBe(["notes.txt"]);
        (await File.ReadAllTextAsync(Path.Combine(repository.RepositoryPath, "notes.txt"), Token).ConfigureAwait(true)).ShouldContain("<<<<<<<", Case.Sensitive);
        result.Message.ShouldContain("conflict markers", Case.Sensitive);
    }

    [Fact]
    public async Task ARunThatChangedNothingHasNothingToApply()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var (applier, _) = await StartAsync(repository).ConfigureAwait(true);

        var result = await applier.ApplyAsync("run-a", repository.RepositoryPath, WorktreeApplyMode.Clean, Token).ConfigureAwait(true);
        var patch = await applier.CreatePatchAsync("run-a", repository.RepositoryPath, Token).ConfigureAwait(true);

        result.Status.ShouldBe(WorktreeApplyStatus.NothingToApply);
        patch.IsEmpty.ShouldBeTrue();
        patch.Text.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListShowsEachRunWithItsChangedFileCount()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var (applier, run) = await StartAsync(repository).ConfigureAwait(true);
        var manager = new WorktreeManager(repository.WorktreeRoot);
        await manager.CreateAsync(repository.RepositoryPath, "run-b", Token).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(run.Path, "a.txt"), "a\n", Token).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(run.Path, "b.txt"), "b\n", Token).ConfigureAwait(true);

        var runs = await applier.ListAsync(repository.RepositoryPath, Token).ConfigureAwait(true);

        runs.Select(summary => (summary.RunId, summary.ChangedFiles)).ShouldBe([("run-a", 2), ("run-b", 0)], ignoreOrder: true);
        runs.ShouldAllBe(summary => summary.Problem == null);
        (await GitCli.RunAsync(run.Path, "diff", "--cached", "--name-only").ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteRemovesTheWorktreeAndItsBranchAndADeletedRunIsNotFound()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var (applier, run) = await StartAsync(repository).ConfigureAwait(true);

        await applier.DeleteAsync("run-a", Token).ConfigureAwait(true);

        Directory.Exists(run.Path).ShouldBeFalse();
        (await GitCli.RunAsync(repository.RepositoryPath, "branch", "--list", "ompos/run/run-a").ConfigureAwait(true)).Trim().ShouldBeEmpty();
        await Should.ThrowAsync<RunNotFoundException>(() => applier.DeleteAsync("run-a", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<RunNotFoundException>(() => applier.CreatePatchAsync("run-a", repository.RepositoryPath, Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task AnInvalidRunIdIsRefusedBeforeAnyPathIsBuilt()
    {
        using var repository = await TempRepository.CreateAsync().ConfigureAwait(true);
        var (applier, _) = await StartAsync(repository).ConfigureAwait(true);

        await Should.ThrowAsync<InvalidRunIdException>(() => applier.CreatePatchAsync("../escape", repository.RepositoryPath, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidRunIdException>(() => applier.DeleteAsync("a/b", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => applier.ApplyAsync("run-a", repository.RepositoryPath, (WorktreeApplyMode)9, Token)).ConfigureAwait(true);
    }
}
