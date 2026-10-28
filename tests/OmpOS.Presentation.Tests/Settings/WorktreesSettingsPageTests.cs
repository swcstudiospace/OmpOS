using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Worktrees;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>The Worktrees page: read-only facts, the setup commands and the four things the setup state can be.</summary>
public sealed class WorktreesSettingsPageTests
{
    private static WorktreesSnapshot With(WorktreeSetupState state, string file = "", string[]? commands = null, string script = "", string error = "") =>
        new("/work/app", 25, state, file, commands ?? [], script, error);

    /// <summary>The page starts loading in its own section and reads nothing before the load.</summary>
    [Fact]
    public void StartsLoadingAndReadsNothing()
    {
        var source = new ScriptedWorktreesSource();

        var page = new WorktreesSettingsPage(source);

        page.State.ShouldBe(SettingsPageState.Loading);
        page.Section.ShouldBe(SettingsSection.Worktrees);
        source.LoadCalls.ShouldBe(0);
    }

    /// <summary>A workspace without a setup file shows the cap as fixed, no commands and the no-setup sentence.</summary>
    [Fact]
    public async Task NoSetupFileShowsTheFactsAndNoCommands()
    {
        var page = new WorktreesSettingsPage(new ScriptedWorktreesSource());

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.WorkspaceRoot.ShouldBe("/work/app");
        page.CapText.ShouldContain("25", Case.Sensitive);
        page.CapText.ShouldContain("fixed in this build", Case.Sensitive);
        page.SetupStateText.ShouldBe(WorktreesCopy.NoSetupFile);
        page.HasSetupFile.ShouldBeFalse();
        page.HasCommands.ShouldBeFalse();
        page.HasSetupError.ShouldBeFalse();
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A setup that was not approved lists its commands and says they will not run until approved; an approved one says so.</summary>
    [Fact]
    public async Task TheConsentStatesAreWordedApart()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedWorktreesSource { Snapshot = With(WorktreeSetupState.NotGranted, "/work/app/.cursor/worktrees.json", ["npm ci", "npm run build"]) };
        var page = new WorktreesSettingsPage(source);

        await page.LoadAsync(token).ConfigureAwait(true);

        page.SetupStateText.ShouldBe("Commands will not run until you approve them when the first worktree is created.");
        page.Commands.ShouldBe(["npm ci", "npm run build"]);
        page.HasCommands.ShouldBeTrue();
        page.HasSetupFile.ShouldBeTrue();

        source.Snapshot = With(WorktreeSetupState.Granted, "/work/app/.cursor/worktrees.json", ["npm ci"]);
        await page.LoadAsync(token).ConfigureAwait(true);

        page.SetupState.ShouldBe(WorktreeSetupState.Granted);
        page.SetupStateText.ShouldBe(WorktreesCopy.Granted);
        page.Commands.ShouldBe(["npm ci"]);
    }

    /// <summary>A script entry shows the script path and no command list.</summary>
    [Fact]
    public async Task AScriptEntryShowsThePath()
    {
        var source = new ScriptedWorktreesSource { Snapshot = With(WorktreeSetupState.NotGranted, "/work/app/.cursor/worktrees.json", script: "/work/app/.cursor/setup.sh") };
        var page = new WorktreesSettingsPage(source);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.HasScript.ShouldBeTrue();
        page.ScriptPath.ShouldBe("/work/app/.cursor/setup.sh");
        page.HasCommands.ShouldBeFalse();
    }

    /// <summary>An invalid file is an error row with the message, not a failed page.</summary>
    [Fact]
    public async Task AnInvalidFileIsAnErrorRowNotAFailedPage()
    {
        var source = new ScriptedWorktreesSource { Snapshot = With(WorktreeSetupState.Invalid, error: "'/work/app/.cursor/worktrees.json' is not valid JSON: bad") };
        var page = new WorktreesSettingsPage(source);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.HasSetupError.ShouldBeTrue();
        page.SetupError.ShouldContain("is not valid JSON", Case.Sensitive);
        page.SetupStateText.ShouldBe(WorktreesCopy.Invalid);
        page.HasCommands.ShouldBeFalse();
    }

    /// <summary>A source that cannot read at all puts the page in its error state with the reason.</summary>
    [Fact]
    public async Task ASourceFailureIsTheErrorState()
    {
        var page = new WorktreesSettingsPage(new ScriptedWorktreesSource { LoadFailure = "the workspace is gone" });

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("the workspace is gone");
    }

    /// <summary>Loading again reads the source again and announces the new facts, so Refresh sees a file that changed.</summary>
    [Fact]
    public async Task LoadingAgainReadsTheSourceAgain()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedWorktreesSource();
        var page = new WorktreesSettingsPage(source);
        await page.LoadAsync(token).ConfigureAwait(true);
        var announced = PropertyChangeRecorder.Record(page);
        source.Snapshot = With(WorktreeSetupState.NotGranted, "/work/app/.cursor/worktrees.json", ["make"]);

        await page.LoadAsync(token).ConfigureAwait(true);

        source.LoadCalls.ShouldBe(2);
        page.Commands.ShouldBe(["make"]);
        announced.ShouldAnnounce(nameof(WorktreesSettingsPage.Commands));
        announced.ShouldAnnounce(nameof(WorktreesSettingsPage.SetupStateText));
    }
}
