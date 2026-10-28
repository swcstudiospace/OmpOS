using Autonogrammer.Presentation.Safety;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Safety;

/// <summary>The Run mode page: states, the three modes, drafts and saving, the lists you can edit and the ones you cannot, and the honest notes.</summary>
public sealed class RunModeSettingsPageTests
{
    private static async Task<(RunModeSettingsPage Page, ScriptedRunModeSource Source)> ReadyAsync(Action<ScriptedRunModeSource>? arrange = null)
    {
        var source = new ScriptedRunModeSource();
        arrange?.Invoke(source);
        var page = new RunModeSettingsPage(SettingsSection.Agents, source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (page, source);
    }

    [Fact]
    public async Task ThePageStartsLoadingThenShowsTheSavedValues()
    {
        var source = new ScriptedRunModeSource();
        var page = new RunModeSettingsPage(SettingsSection.Agents, source);
        page.State.ShouldBe(SettingsPageState.Loading);
        page.Title.ShouldBe(SafetyCopy.Title);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.Mode.ShouldBe(RunModeChoice.AutoReview);
        page.IsDirty.ShouldBeFalse();
        page.TerminalRows.Select(row => (row.Text, row.IsEditable)).ToArray().ShouldBe([("npm", false), ("git status", true)]);
        page.TerminalRows[0].Source.ShouldBe("/home/u/.autonogrammer/permissions.json");
        page.TerminalRows[1].Source.ShouldBe("/w/.autonogrammer/permissions.json");
    }

    [Fact]
    public async Task AFailedLoadIsAnErrorStateNotAnException()
    {
        var (page, _) = await ReadyAsync(source => source.LoadFailure = "the mode store is unreadable");

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("the mode store is unreadable");
        page.IsDirty.ShouldBeFalse();
        page.SetMode(RunModeChoice.Allowlist);
        page.Mode.ShouldBe(RunModeChoice.AutoReview, "there is no draft while the page is not ready");
    }

    [Fact]
    public void ThePageListsThreeModesWithTheirHonestSentences()
    {
        RunModeSettingsPage.Options.Select(option => option.Choice).ToArray().ShouldBe([RunModeChoice.AutoReview, RunModeChoice.Allowlist, RunModeChoice.RunEverything]);
        RunModeSettingsPage.Options.Select(option => option.Title).ToArray().ShouldBe(["Auto-review", "Allowlist", "Run Everything"]);
        RunModeSettingsPage.Options.ShouldAllBe(option => option.Summary.Length > 0);
        RunModeSettingsPage.Options[0].Summary.ShouldContain("classifier", Case.Sensitive);
        RunModeSettingsPage.Options[2].Summary.ShouldContain("deny rules", Case.Sensitive);
        SafetyCopy.ClassifierNote.ShouldContain("Autonogrammer has no hosted model", Case.Sensitive);
        SafetyCopy.NoSandboxNote.ShouldContain("no operating-system sandbox", Case.Sensitive);
    }

    [Fact]
    public async Task ChoosingAModeIsADraftUntilSaveAndTheNotesFollowIt()
    {
        var (page, source) = await ReadyAsync();
        var announced = PropertyChangeRecorder.Record(page);
        page.IsClassifierNoteVisible.ShouldBeTrue();
        page.IsRunEverythingWarningVisible.ShouldBeFalse();
        page.IsReadAccessVisible.ShouldBeTrue();

        page.SetMode(RunModeChoice.RunEverything);

        page.Mode.ShouldBe(RunModeChoice.RunEverything);
        page.IsDirty.ShouldBeTrue();
        page.IsRunEverythingWarningVisible.ShouldBeTrue();
        page.IsClassifierNoteVisible.ShouldBeFalse();
        page.IsReadAccessVisible.ShouldBeFalse("read access does not apply in Run Everything");
        page.ModeSummary.ShouldBe(SafetyCopy.RunEverythingSummary);
        page.EffectiveModeText.ShouldContain("Run Everything", Case.Sensitive);
        announced.ShouldAnnounce(nameof(RunModeSettingsPage.Mode));
        announced.ShouldAnnounce(nameof(RunModeSettingsPage.IsRunEverythingWarningVisible));
        source.Saved.ShouldBeEmpty();

        page.SetMode(RunModeChoice.Allowlist);
        page.IsClassifierNoteVisible.ShouldBeFalse("the classifier note explains Auto-review only");

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        source.Saved.ShouldHaveSingleItem().Mode.ShouldBe(RunModeChoice.Allowlist);
        page.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task ChoosingTheSameModeTwiceOrGoingBackIsNotAChange()
    {
        var (page, _) = await ReadyAsync();

        page.SetMode(RunModeChoice.AutoReview);
        page.IsDirty.ShouldBeFalse();
        page.SetMode(RunModeChoice.Allowlist);
        page.SetMode(RunModeChoice.AutoReview);
        page.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task AnAddedEntryAppearsAsARemovableRowAndASavedOneIsWrittenThroughTheSource()
    {
        var (page, source) = await ReadyAsync();

        page.AddTerminalEntry("  npm:install*  ").ShouldBeTrue();
        page.AddMcpEntry("github:*").ShouldBeTrue();

        page.IsDirty.ShouldBeTrue();
        page.TerminalRows.Select(row => row.Text).ToArray().ShouldBe(["npm", "git status", "npm:install*"]);
        page.McpRows.ShouldHaveSingleItem().IsEditable.ShouldBeTrue();
        page.HasEntryError.ShouldBeFalse();

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        var saved = source.Saved.ShouldHaveSingleItem();
        saved.TerminalAllowlist.ShouldBe(["git status", "npm:install*"]);
        saved.McpAllowlist.ShouldBe(["github:*"]);
        source.LoadCalls.ShouldBe(2, "a save reads the files again so the rows show what is in force");
    }

    [Fact]
    public async Task OnlyEntriesOfTheEditedFileCanBeRemoved()
    {
        var (page, _) = await ReadyAsync();

        page.RemoveTerminalEntry("npm").ShouldBeFalse("it belongs to another file");
        page.TerminalRows.Count.ShouldBe(2);

        page.RemoveTerminalEntry("git status").ShouldBeTrue();

        page.TerminalRows.Select(row => row.Text).ToArray().ShouldBe(["npm"]);
        page.IsDirty.ShouldBeTrue();
        page.RemoveTerminalEntry("git status").ShouldBeFalse("it is already gone");
    }

    [Theory]
    [InlineData("", "Write an entry first.")]
    [InlineData("   ", "Write an entry first.")]
    [InlineData("git status", "That entry is already in the list.")]
    [InlineData("a\nb", "An entry must be one line.")]
    public async Task ARejectedTerminalEntryChangesNothingAndSaysWhy(string text, string message)
    {
        var (page, _) = await ReadyAsync();
        var announced = PropertyChangeRecorder.Record(page);

        page.AddTerminalEntry(text).ShouldBeFalse();

        page.EntryError.ShouldBe(message);
        page.HasEntryError.ShouldBeTrue();
        page.IsDirty.ShouldBeFalse();
        announced.ShouldAnnounce(nameof(RunModeSettingsPage.EntryError));
    }

    [Fact]
    public async Task AnEntryTooLongIsRejectedAndTheNextGoodEntryClearsTheMessage()
    {
        var (page, _) = await ReadyAsync();

        page.AddTerminalEntry(new string('a', 513)).ShouldBeFalse();
        page.EntryError.ShouldBe(SafetyCopy.EntryTooLong);

        page.AddTerminalEntry("make").ShouldBeTrue();
        page.EntryError.ShouldBeEmpty();
        page.HasEntryError.ShouldBeFalse();
    }

    [Theory]
    [InlineData("github")]
    [InlineData(":tool")]
    [InlineData("server:")]
    public async Task AnMcpEntryNeedsServerAndTool(string text)
    {
        var (page, _) = await ReadyAsync();

        page.AddMcpEntry(text).ShouldBeFalse();

        page.EntryError.ShouldBe(SafetyCopy.McpEntryInvalid);
        page.McpRows.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/opt/shared", true)]
    [InlineData("~/notes/*.md", true)]
    [InlineData("C:\\data\\x", true)]
    [InlineData("C:/data/x", true)]
    [InlineData("relative/dir", false)]
    [InlineData("*.md", false)]
    public async Task AReadPathMustBeAbsoluteOrStartWithATilde(string text, bool accepted)
    {
        var (page, _) = await ReadyAsync();

        page.AddReadPath(text).ShouldBe(accepted);

        page.EntryError.ShouldBe(accepted ? string.Empty : SafetyCopy.ReadPathRelative);
    }

    [Fact]
    public async Task TheReadBoundaryIsADraftAndItsSourceIsNamed()
    {
        var (page, source) = await ReadyAsync(s => s.Snapshot = s.Snapshot with { ReadBoundarySource = "/home/u/.cursor/sandbox.json" });

        page.ReadBoundary.ShouldBe(ReadBoundaryChoice.System);
        page.BoundarySourceText.ShouldBe("The read boundary is controlled by /home/u/.cursor/sandbox.json.");

        page.SetReadBoundary(ReadBoundaryChoice.Workspace);
        page.IsDirty.ShouldBeTrue();
        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        source.Saved.ShouldHaveSingleItem().ReadBoundary.ShouldBe(ReadBoundaryChoice.Workspace);
    }

    [Fact]
    public async Task WithNoSandboxFileTheSettingsValueApplies()
    {
        var (page, _) = await ReadyAsync();

        page.BoundarySourceText.ShouldContain("comes from this page", Case.Sensitive);
        page.ReadPathsSourceText.ShouldContain("comes from this page", Case.Sensitive);
    }

    [Fact]
    public async Task TheReadAllowlistShowsTheOneInForceAndYourOwnEntriesOnTop()
    {
        var (page, _) = await ReadyAsync(s => s.Snapshot = s.Snapshot with
        {
            EffectiveReadPaths = [new RunModeEntry("/opt/user", "/home/u/.autonogrammer/sandbox.json", IsEditable: false)],
            ReadPathsSource = "/home/u/.autonogrammer/sandbox.json",
        });

        page.ReadPathRows.Select(row => (row.Text, row.IsEditable)).ToArray().ShouldBe([("/opt/user", false)]);

        page.AddReadPath("/opt/mine").ShouldBeTrue();

        page.ReadPathRows.Select(row => (row.Text, row.IsEditable)).ToArray().ShouldBe([("/opt/user", false), ("/opt/mine", true)]);
    }

    [Fact]
    public async Task WhenTheEditedFileSetsTheReadAllowlistOnlyItsEntriesShow()
    {
        var (page, _) = await ReadyAsync(s => s.Snapshot = s.Snapshot with
        {
            Values = s.Snapshot.Values with { AdditionalReadPaths = ["/opt/mine"] },
            EffectiveReadPaths = [new RunModeEntry("/opt/mine", "/w/.autonogrammer/sandbox.json", IsEditable: false)],
            ReadPathsSource = "/w/.autonogrammer/sandbox.json",
        });

        page.ReadPathRows.Select(row => (row.Text, row.IsEditable)).ToArray().ShouldBe([("/opt/mine", true)]);
        page.RemoveReadPath("/opt/mine").ShouldBeTrue();
        page.ReadPathRows.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithNoFileToWriteEntriesAndTheBoundaryCannotBeEdited()
    {
        var (page, _) = await ReadyAsync(s => s.Snapshot = s.Snapshot with { EditPermissionsPath = string.Empty, EditSandboxPath = string.Empty });

        page.CanEditEntries.ShouldBeFalse();
        page.CanEditSandbox.ShouldBeFalse();
        page.AddTerminalEntry("make").ShouldBeFalse();
        page.EntryError.ShouldBe(SafetyCopy.NoTarget);
        page.AddReadPath("/opt/x").ShouldBeFalse();
        page.SetReadBoundary(ReadBoundaryChoice.Workspace);
        page.ReadBoundary.ShouldBe(ReadBoundaryChoice.System);
        page.PermissionsTargetText.ShouldBe(SafetyCopy.SavedTo(string.Empty));
        page.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task ARejectedSaveKeepsTheDraftShowsTheReasonAndWritesNothing()
    {
        var (page, source) = await ReadyAsync();
        source.SaveFailure = "/w/.autonogrammer/permissions.json has comments. Edit it by hand.";
        page.AddTerminalEntry("make");

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        page.HasError.ShouldBeTrue();
        page.SaveError.ShouldBe("/w/.autonogrammer/permissions.json has comments. Edit it by hand.");
        page.IsDirty.ShouldBeTrue();
        page.TerminalRows.Select(row => row.Text).ToArray().ShouldBe(["npm", "git status", "make"]);
        source.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task DiscardReturnsToTheSavedValuesAndClearsTheEntryMessage()
    {
        var (page, _) = await ReadyAsync();
        page.AddTerminalEntry("make");
        page.AddTerminalEntry("");
        page.SetMode(RunModeChoice.RunEverything);

        page.Discard();

        page.IsDirty.ShouldBeFalse();
        page.Mode.ShouldBe(RunModeChoice.AutoReview);
        page.TerminalRows.Select(row => row.Text).ToArray().ShouldBe(["npm", "git status"]);
        page.HasEntryError.ShouldBeFalse();
    }

    [Fact]
    public async Task ResetReturnsTheDraftToTheDefaultsWithoutSaving()
    {
        var (page, source) = await ReadyAsync();
        page.SetMode(RunModeChoice.Allowlist);
        await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Reset();

        page.Mode.ShouldBe(RunModeChoice.AutoReview);
        page.IsDirty.ShouldBeTrue("the defaults differ from what is saved");
        page.TerminalRows.Select(row => row.Text).ToArray().ShouldBe(["npm"]);
        source.Saved.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TheFilesRulesInstructionsAndProblemsAreShownAsRead()
    {
        var (page, _) = await ReadyAsync(s => s.Snapshot = s.Snapshot with
        {
            AllowRules = [new RunModeEntry("Read(src/**)", "/w/.autonogrammer/permissions.json", IsEditable: false)],
            DenyRules = [new RunModeEntry("Shell(rm)", "/home/u/.autonogrammer/permissions.json", IsEditable: false)],
            AllowInstructions = [new RunModeEntry("Reads under dist are fine.", "/w/.cursor/permissions.json", IsEditable: false)],
            Files = [new RunModeFileRow("/w/.autonogrammer/sandbox.json", SafetyCopy.ScopeProject, "sandbox.json", "Read.", IsWinning: true)],
            Diagnostics = [new RunModeDiagnostic("/home/u/.cursor/permissions.json", "It is not valid JSON. It is ignored.", IsError: true)],
            ProjectTrusted = false,
        });

        page.AllowRules.Single().Text.ShouldBe("Read(src/**)");
        page.DenyRules.Single().Summary.ShouldBe("Shell(rm), from /home/u/.autonogrammer/permissions.json");
        page.HasInstructions.ShouldBeTrue();
        page.AllowInstructions.Count.ShouldBe(1);
        page.BlockInstructions.ShouldBeEmpty();
        page.Files.Single().Summary.ShouldContain("It sets a value in force.", Case.Sensitive);
        page.HasDiagnostics.ShouldBeTrue();
        page.Diagnostics.Single().Summary.ShouldStartWith("Error in /home/u/.cursor/permissions.json", Case.Sensitive);
        page.IsProjectUntrusted.ShouldBeTrue();
    }

    [Fact]
    public async Task WithNoProblemTheListIsEmptyAndSaysSo()
    {
        var (page, _) = await ReadyAsync();

        page.HasDiagnostics.ShouldBeFalse();
        page.HasInstructions.ShouldBeFalse();
        page.IsProjectUntrusted.ShouldBeFalse();
        SafetyCopy.NoProblems.ShouldContain("No problems", Case.Sensitive);
    }

    [Fact]
    public void ValuesAreEqualWhenTheirListsHoldTheSameEntriesInTheSameOrder()
    {
        var left = new RunModeValues(RunModeChoice.Allowlist, ReadBoundaryChoice.Workspace, ["a", "b"], ["x:y"], ["/p"]);
        var same = new RunModeValues(RunModeChoice.Allowlist, ReadBoundaryChoice.Workspace, ["a", "b"], ["x:y"], ["/p"]);

        left.ShouldBe(same);
        left.GetHashCode().ShouldBe(same.GetHashCode());
        left.ShouldNotBe(left with { TerminalAllowlist = ["b", "a"] });
        left.ShouldNotBe(left with { Mode = RunModeChoice.AutoReview });
        left.ShouldNotBe(left with { AdditionalReadPaths = [] });
    }

    [Fact]
    public void TheConstructorRejectsANullSource()
    {
        Should.Throw<ArgumentNullException>(() => new RunModeSettingsPage(SettingsSection.Agents, null!));
    }
}
