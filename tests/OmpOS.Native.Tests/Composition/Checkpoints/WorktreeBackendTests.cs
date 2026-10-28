using System.ComponentModel;
using System.Diagnostics;
using OmpOS.Native.Composition.Checkpoints;
using OmpOS.Presentation.Worktrees;
using OmpOS.Worktrees;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Composition.Checkpoints;

/// <summary>The worktree adapter over a real temporary git repository: start, change, preview, apply, delete, and the refusal of a folder that is no repository.</summary>
public sealed class WorktreeBackendTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ompos-wtb-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the temporary directory, clearing read-only attributes git sets on object files.</summary>
    public void Dispose()
    {
        if (!Directory.Exists(_root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    private static bool GitIsAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "--version") { RedirectStandardOutput = true, UseShellExecute = false });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static async Task GitAsync(string directory, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = info };
        process.Start();
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        _ = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {await error.ConfigureAwait(true)}");
        }
    }

    private async Task<string> CreateRepositoryAsync()
    {
        Assert.SkipUnless(GitIsAvailable(), "git is not on PATH.");
        var repository = Path.Combine(_root, "repo");
        Directory.CreateDirectory(repository);
        await GitAsync(repository, "init", "--quiet", "--initial-branch=main").ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(repository, "a.txt"), "seed\n", TestContext.Current.CancellationToken).ConfigureAwait(true);
        await GitAsync(repository, "add", "a.txt").ConfigureAwait(true);
        await GitAsync(repository, "-c", "user.name=OmpOS Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "seed").ConfigureAwait(true);
        return repository;
    }

    /// <summary>A run is created, its changes are previewed as a patch and applied to the repository, and deleting it removes the worktree.</summary>
    [Fact]
    public async Task ARunIsStartedChangedPreviewedAppliedAndDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await CreateRepositoryAsync().ConfigureAwait(true);
        var backend = new WorktreeBackend(Path.Combine(_root, "state"), () => repository, new FixedSetupRunner("setup said hi"));

        var started = await backend.StartAsync("run-1", ct).ConfigureAwait(true);
        Directory.Exists(started.Run.Path).ShouldBeTrue();
        started.Run.Branch.ShouldBe("ompos/run/run-1", Case.Sensitive);
        started.SetupMessage.ShouldBe("setup said hi", Case.Sensitive);

        await File.WriteAllTextAsync(Path.Combine(started.Run.Path, "a.txt"), "changed\n", ct).ConfigureAwait(true);
        var listed = await backend.ListAsync(ct).ConfigureAwait(true);
        var preview = await backend.PreviewAsync("run-1", ct).ConfigureAwait(true);
        var outcome = await backend.ApplyAsync("run-1", WorktreeApplyMethod.Clean, ct).ConfigureAwait(true);

        listed.Single(run => run.RunId == "run-1").ChangedFiles.ShouldBe(1);
        preview.Files.Count.ShouldBe(1);
        preview.Files[0].Path.ShouldBe("a.txt", Case.Sensitive);
        preview.Files[0].Change.ShouldBe(WorktreeFileChange.Modified);
        preview.Added.ShouldBe(1);
        preview.Removed.ShouldBe(1);
        preview.Text.ShouldContain("+changed", Case.Sensitive);
        outcome.State.ShouldBe(WorktreeApplyState.Applied);
        (await File.ReadAllTextAsync(Path.Combine(repository, "a.txt"), ct).ConfigureAwait(true)).ShouldBe("changed\n", Case.Sensitive);

        await backend.DeleteAsync("run-1", ct).ConfigureAwait(true);

        Directory.Exists(started.Run.Path).ShouldBeFalse();
        (await backend.ListAsync(ct).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>A run that changed nothing has nothing to apply.</summary>
    [Fact]
    public async Task AnUnchangedRunHasNothingToApply()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await CreateRepositoryAsync().ConfigureAwait(true);
        var backend = new WorktreeBackend(Path.Combine(_root, "state"), () => repository, new FixedSetupRunner(string.Empty));
        await backend.StartAsync("run-2", ct).ConfigureAwait(true);

        var outcome = await backend.ApplyAsync("run-2", WorktreeApplyMethod.ThreeWay, ct).ConfigureAwait(true);

        outcome.State.ShouldBe(WorktreeApplyState.NothingToApply);
    }

    /// <summary>A folder that is not in a git repository is refused with a message a user can read, before any git command runs.</summary>
    [Fact]
    public async Task AFolderOutsideAGitRepositoryIsRefusedWithAReadableMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var plain = Path.Combine(_root, "plain");
        Directory.CreateDirectory(plain);
        var backend = new WorktreeBackend(Path.Combine(_root, "state"), () => plain, new FixedSetupRunner(string.Empty));

        var failure = await Should.ThrowAsync<InvalidOperationException>(() => backend.StartAsync("run-3", ct)).ConfigureAwait(true);

        failure.Message.ShouldContain("not inside a git repository", Case.Sensitive);
    }

    /// <summary>No open workspace is refused with a message that says what to do.</summary>
    [Fact]
    public async Task NoWorkspaceRootIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var backend = new WorktreeBackend(Path.Combine(_root, "state"), static () => null, new FixedSetupRunner(string.Empty));

        var failure = await Should.ThrowAsync<InvalidOperationException>(() => backend.ListAsync(ct)).ConfigureAwait(true);

        failure.Message.ShouldContain("workspace folder", Case.Sensitive);
    }

    private sealed class FixedSetupRunner(string message) : IWorktreeSetupRunner
    {
        public Task<WorktreeSetupResult> RunAsync(WorktreeSetupRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new WorktreeSetupResult(WorktreeSetupStatus.Completed, 1, message));
    }
}
