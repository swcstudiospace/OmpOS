using Shouldly;
using Xunit;

namespace Autonogrammer.Worktrees.Tests;

/// <summary>Verifies that a worktree's setup never runs without consent, that a changed file needs new consent, and how the shell executor runs commands.</summary>
public sealed class WorktreeSetupRunnerTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "autonogrammer-wt-setup-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Project => Path.Combine(_base, "project");

    private string Worktree => Path.Combine(_base, "worktree");

    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }

    private void WriteConfig(string json, string? directory = null)
    {
        var folder = Path.Combine(directory ?? Project, ".cursor");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "worktrees.json"), json);
        Directory.CreateDirectory(Worktree);
    }

    [Fact]
    public async Task ARepositoryWithoutAConfigurationHasNoSetupAndNobodyIsAsked()
    {
        Directory.CreateDirectory(Project);
        Directory.CreateDirectory(Worktree);
        var consent = new ScriptedWorktreeSetupConsent(answer: true);
        var executor = new RecordingWorktreeSetupExecutor();
        var runner = new ConsentedWorktreeSetupRunner(consent, executor, windows: false);

        var result = await runner.RunAsync(new WorktreeSetupRequest(Worktree, Project), Token).ConfigureAwait(true);

        result.Status.ShouldBe(WorktreeSetupStatus.NoSetup);
        consent.Asked.ShouldBeEmpty();
        executor.Executions.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithoutConsentNothingRunsAndTheUserSawTheCommands()
    {
        WriteConfig("""{ "setup-worktree": ["npm ci", "cp $ROOT_WORKTREE_PATH/.env .env"] }""");
        var consent = new ScriptedWorktreeSetupConsent(answer: false);
        var executor = new RecordingWorktreeSetupExecutor();
        var runner = new ConsentedWorktreeSetupRunner(consent, executor, windows: false);

        var result = await runner.RunAsync(new WorktreeSetupRequest(Worktree, Project), Token).ConfigureAwait(true);

        result.Status.ShouldBe(WorktreeSetupStatus.ConsentDenied);
        executor.Executions.ShouldBeEmpty();
        var request = consent.Asked.ShouldHaveSingleItem();
        request.Setup.Commands.ShouldBe(["npm ci", "cp $ROOT_WORKTREE_PATH/.env .env"]);
        request.Identity.StoreRepositoryPath.ShouldBe(Path.Combine(Path.GetFullPath(Project), ".cursor", "worktrees.json"));
    }

    [Fact]
    public async Task WithConsentTheExecutorGetsTheCommandsTheWorktreeAndTheProjectRoot()
    {
        WriteConfig("""{ "setup-worktree": ["echo one"] }""");
        var executor = new RecordingWorktreeSetupExecutor();
        var runner = new ConsentedWorktreeSetupRunner(new ScriptedWorktreeSetupConsent(answer: true), executor, windows: false);

        var result = await runner.RunAsync(new WorktreeSetupRequest(Worktree, Project), Token).ConfigureAwait(true);

        result.Status.ShouldBe(WorktreeSetupStatus.Completed);
        var execution = executor.Executions.ShouldHaveSingleItem();
        execution.WorktreePath.ShouldBe(Worktree);
        execution.ProjectRoot.ShouldBe(Project);
        execution.Setup.Commands.ShouldBe(["echo one"]);
    }

    [Fact]
    public async Task AChangedConfigurationOrScriptIsADifferentIdentityAndTheSystemKeyWins()
    {
        WriteConfig("""{ "setup-worktree": ["echo generic"], "setup-worktree-unix": ["echo unix"], "setup-worktree-windows": ["echo windows"] }""");
        var unix = new ScriptedWorktreeSetupConsent(answer: false);
        var windows = new ScriptedWorktreeSetupConsent(answer: false);

        await new ConsentedWorktreeSetupRunner(unix, new RecordingWorktreeSetupExecutor(), windows: false).RunAsync(new WorktreeSetupRequest(Worktree, Project), Token).ConfigureAwait(true);
        await new ConsentedWorktreeSetupRunner(windows, new RecordingWorktreeSetupExecutor(), windows: true).RunAsync(new WorktreeSetupRequest(Worktree, Project), Token).ConfigureAwait(true);

        unix.Asked.Single().Setup.Commands.ShouldBe(["echo unix"]);
        windows.Asked.Single().Setup.Commands.ShouldBe(["echo windows"]);

        WriteConfig("""{ "setup-worktree": ["echo generic"], "setup-worktree-unix": ["echo changed"] }""");
        var again = new ScriptedWorktreeSetupConsent(answer: false);
        await new ConsentedWorktreeSetupRunner(again, new RecordingWorktreeSetupExecutor(), windows: false).RunAsync(new WorktreeSetupRequest(Worktree, Project), Token).ConfigureAwait(true);

        again.Asked.Single().Identity.Sha256.ShouldNotBe(unix.Asked.Single().Identity.Sha256, StringComparer.Ordinal);
        again.Asked.Single().Identity.Sha256.Length.ShouldBe(64);
    }

    [Fact]
    public async Task AScriptEntryIsPartOfTheIdentityAndTheWorktreesOwnFileWinsOverTheProjects()
    {
        WriteConfig("""{ "setup-worktree": "setup.sh" }""");
        await File.WriteAllTextAsync(Path.Combine(Project, ".cursor", "setup.sh"), "echo a\n", Token).ConfigureAwait(true);
        var first = new ScriptedWorktreeSetupConsent(answer: false);
        var runner = new ConsentedWorktreeSetupRunner(first, new RecordingWorktreeSetupExecutor(), windows: false);
        await runner.RunAsync(new WorktreeSetupRequest(Worktree, Project), Token).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(Project, ".cursor", "setup.sh"), "echo evil\n", Token).ConfigureAwait(true);
        var second = new ScriptedWorktreeSetupConsent(answer: false);
        await new ConsentedWorktreeSetupRunner(second, new RecordingWorktreeSetupExecutor(), windows: false).RunAsync(new WorktreeSetupRequest(Worktree, Project), Token).ConfigureAwait(true);

        first.Asked.Single().Setup.IsScript.ShouldBeTrue();
        second.Asked.Single().Identity.Sha256.ShouldNotBe(first.Asked.Single().Identity.Sha256, StringComparer.Ordinal);

        WriteConfig("""{ "setup-worktree": ["echo own"] }""", Worktree);
        var own = new ScriptedWorktreeSetupConsent(answer: false);
        await new ConsentedWorktreeSetupRunner(own, new RecordingWorktreeSetupExecutor(), windows: false).RunAsync(new WorktreeSetupRequest(Worktree, Project), Token).ConfigureAwait(true);

        own.Asked.Single().Setup.Commands.ShouldBe(["echo own"]);
    }

    [Fact]
    public async Task AMalformedConfigurationIsReportedAndNobodyIsAsked()
    {
        WriteConfig("{ not json");
        var consent = new ScriptedWorktreeSetupConsent(answer: true);
        var executor = new RecordingWorktreeSetupExecutor();
        var runner = new ConsentedWorktreeSetupRunner(consent, executor, windows: false);

        var result = await runner.RunAsync(new WorktreeSetupRequest(Worktree, Project), Token).ConfigureAwait(true);

        result.Status.ShouldBe(WorktreeSetupStatus.Invalid);
        consent.Asked.ShouldBeEmpty();
        executor.Executions.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheShellExecutorRunsEachCommandInTheWorktreeWithTheRootWorktreePathAndStopsAtTheFirstFailure()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The POSIX shell is not available on Windows.");
        Directory.CreateDirectory(Project);
        Directory.CreateDirectory(Worktree);
        var setup = new CursorWorktreeSetup(["printf '%s' \"$ROOT_WORKTREE_PATH\" > root.txt", "echo broken >&2; exit 3", "touch never.txt"], ScriptPath: null);

        var result = await new ShellWorktreeSetupExecutor(windows: false).ExecuteAsync(new WorktreeSetupExecution(Worktree, Project, setup), Token).ConfigureAwait(true);

        result.Status.ShouldBe(WorktreeSetupStatus.Failed);
        result.CommandsRun.ShouldBe(2);
        result.Message.ShouldContain("exited with code 3", Case.Sensitive);
        result.Message.ShouldContain("broken", Case.Sensitive);
        (await File.ReadAllTextAsync(Path.Combine(Worktree, "root.txt"), Token).ConfigureAwait(true)).ShouldBe(Project);
        File.Exists(Path.Combine(Worktree, "never.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task TheShellExecutorRunsAScriptEntryAndReportsCompletion()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The POSIX shell is not available on Windows.");
        Directory.CreateDirectory(Project);
        Directory.CreateDirectory(Worktree);
        var script = Path.Combine(Project, "setup it.sh");
        await File.WriteAllTextAsync(script, "touch ran.txt\n", Token).ConfigureAwait(true);
        var setup = new CursorWorktreeSetup([], script);

        var result = await new ShellWorktreeSetupExecutor(windows: false).ExecuteAsync(new WorktreeSetupExecution(Worktree, Project, setup), Token).ConfigureAwait(true);

        result.Status.ShouldBe(WorktreeSetupStatus.Completed);
        result.CommandsRun.ShouldBe(1);
        File.Exists(Path.Combine(Worktree, "ran.txt")).ShouldBeTrue();
    }
}
