using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Workspaces;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings.Workspaces;

/// <summary>The Workspaces page: loading, the draft in the picker, dirty tracking, save rules, reset, discard, and the recent sets.</summary>
public sealed class WorkspacesSettingsPageTests
{
    private static async Task<(WorkspacesSettingsPage Page, ScriptedWorkspacesSource Source)> OpenAsync(Action<ScriptedWorkspacesSource>? configure = null)
    {
        var source = new ScriptedWorkspacesSource();
        configure?.Invoke(source);
        var page = new WorkspacesSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (page, source);
    }

    /// <summary>A loaded page shows the recent sets and an empty editor named Workspace; nothing is dirty and nothing is announced.</summary>
    [Fact]
    public async Task LoadingListsTheRecentsAndStartsWithAnEmptyEditor()
    {
        var (page, _) = await OpenAsync(source =>
        {
            source.Recents.Add(ScriptedWorkspacesSource.Row("Backend", "/work/api", "/work/lib"));
            source.Recents.Add(ScriptedWorkspacesSource.Row("Docs", "/work/docs"));
        }).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.Section.ShouldBe(SettingsSection.Workspaces);
        page.Title.ShouldBe(WorkspacesCopy.Title);
        page.Recents.Select(row => row.Name).ShouldBe(["Backend", "Docs"]);
        page.HasRecents.ShouldBeTrue();
        page.HasNoRecents.ShouldBeFalse();
        page.Picker.Name.ShouldBe(WorkspacesCopy.DefaultName);
        page.Picker.Rows.ShouldBeEmpty();
        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();
        page.Announcement.ShouldBeEmpty();
    }

    /// <summary>With no saved set the page says so instead of drawing an empty list.</summary>
    [Fact]
    public async Task NoRecentsIsAStateOfItsOwn()
    {
        var (page, _) = await OpenAsync().ConfigureAwait(true);

        page.HasRecents.ShouldBeFalse();
        page.HasNoRecents.ShouldBeTrue();
    }

    /// <summary>A list that cannot be read puts the page in its error state with the source's sentence; nothing is dirty and no recent state is claimed.</summary>
    [Fact]
    public async Task AFailedLoadShowsTheErrorState()
    {
        var (page, source) = await OpenAsync(s => s.ListFailure = "The file is locked.").ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("The file is locked.");
        page.IsDirty.ShouldBeFalse();
        page.HasNoRecents.ShouldBeFalse();

        source.ListFailure = null;
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.HasNoRecents.ShouldBeTrue();
    }

    /// <summary>The page is dirty while the picker differs from the loaded set, and clean again when the edits are undone; the page announces both.</summary>
    [Fact]
    public async Task TheSetInThePickerIsTheDraft()
    {
        var (page, _) = await OpenAsync().ConfigureAwait(true);
        var announced = PropertyChangeRecorder.Record(page);

        page.Picker.AddFolder("/work/api");

        page.IsDirty.ShouldBeTrue();
        announced.ShouldAnnounce(nameof(WorkspacesSettingsPage.IsDirty));

        page.Picker.Remove("api").ShouldBeTrue();
        page.IsDirty.ShouldBeFalse();

        page.Picker.Name = "Backend";
        page.IsDirty.ShouldBeTrue();
        page.Picker.Name = WorkspacesCopy.DefaultName;
        page.IsDirty.ShouldBeFalse();

        page.Picker.AddFolder("/work/api");
        page.Picker.AddFolder("/work/lib");
        page.Picker.Move("lib", -1).ShouldBeFalse("a folder next to the primary has nowhere to move");
        page.Picker.MakePrimary("lib").ShouldBeTrue();
        page.IsDirty.ShouldBeTrue();
    }

    /// <summary>A page whose reload failed has no draft: it is not dirty and refuses to load a recent set, to forget one, to reset or to discard, whatever the editor still holds.</summary>
    [Fact]
    public async Task AFailedReloadLeavesNothingToEdit()
    {
        var (page, source) = await OpenAsync(s => s.Recents.Add(ScriptedWorkspacesSource.Row("Docs", "/work/docs"))).ConfigureAwait(true);
        page.Picker.AddFolder("/work/api");
        page.IsDirty.ShouldBeTrue();
        source.ListFailure = "The file is locked.";

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.IsDirty.ShouldBeFalse("a page that failed to load has no draft to be dirty");
        page.LoadRecentIntoEditor("docs").ShouldBeFalse();
        (await page.ForgetRecentAsync("docs", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();
        page.Reset();
        page.Discard();

        page.Picker.Rows.Select(row => row.Id).ShouldBe(["api"], "reset and discard do nothing while the page is not ready");
        source.Recents.ShouldHaveSingleItem("nothing was forgotten");
    }

    /// <summary>A set with a blocking finding is not saved: the page says which finding, the source is never called and the page stays dirty.</summary>
    [Fact]
    public async Task SavingIsRefusedWhileASetHasAnError()
    {
        var (page, source) = await OpenAsync().ConfigureAwait(true);
        page.Picker.AddFolder("/work/api");
        page.Picker.AddFolder("/work/api");

        var saved = await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        saved.ShouldBeFalse();
        page.HasError.ShouldBeTrue();
        page.SaveError.ShouldContain("same folder as an earlier root", Case.Sensitive);
        page.SaveError.ShouldStartWith("The set cannot be saved yet.", Case.Sensitive);
        page.IsDirty.ShouldBeTrue();
        source.SavedNames.ShouldBeEmpty();

        page.Picker.Remove("api-2").ShouldBeTrue();
        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        page.SaveError.ShouldBeEmpty();
        source.SavedNames.ShouldBe(["Workspace"]);
    }

    /// <summary>A set with a name but no folder cannot be saved: the page asks for a folder.</summary>
    [Fact]
    public async Task ASetWithoutFoldersIsNotSaved()
    {
        var (page, source) = await OpenAsync().ConfigureAwait(true);
        page.Picker.Name = "Backend";

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        page.SaveError.ShouldContain(WorkspacesCopy.NoFolders, Case.Sensitive);
        source.SavedNames.ShouldBeEmpty();
    }

    /// <summary>A warning does not block a save.</summary>
    [Fact]
    public async Task AWarningDoesNotBlockSaving()
    {
        var (page, source) = await OpenAsync().ConfigureAwait(true);
        page.Picker.AddFolder("/work/notes");

        page.Picker.HasWarnings.ShouldBeTrue();
        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        source.SavedNames.ShouldHaveSingleItem();
    }

    /// <summary>Saving hands the name and the folders to the source, makes the page clean, lists the set first in the recents and announces it; saving again does nothing.</summary>
    [Fact]
    public async Task SavingRecordsTheSetAndRefreshesTheRecents()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source) = await OpenAsync(s => s.Recents.Add(ScriptedWorkspacesSource.Row("Docs", "/work/docs"))).ConfigureAwait(true);
        page.Picker.AddFolder("/work/api");
        page.Picker.AddFolder("/work/lib");
        page.Picker.Name = "Backend";

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        source.SavedNames.ShouldBe(["Backend"]);
        source.SavedRoots.Select(root => (root.Path, root.IsPrimary)).ShouldBe([("/work/api", true), ("/work/lib", false)]);
        page.IsDirty.ShouldBeFalse();
        page.SaveError.ShouldBeEmpty();
        page.Recents.Select(row => row.Name).ShouldBe(["Backend", "Docs"]);
        page.Announcement.ShouldBe(WorkspacesCopy.Saved("Backend"));

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        source.SavedNames.ShouldHaveSingleItem();
    }

    /// <summary>A failed write keeps the draft, shows the source's reason and lists nothing new; the next save succeeds and clears the reason.</summary>
    [Fact]
    public async Task AFailedSaveKeepsTheDraftAndShowsTheReason()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source) = await OpenAsync(s => s.SaveFailure = "The disk is full.").ConfigureAwait(true);
        page.Picker.AddFolder("/work/api");

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();

        page.SaveError.ShouldBe("The disk is full.");
        page.HasError.ShouldBeTrue();
        page.IsDirty.ShouldBeTrue();
        page.Recents.ShouldBeEmpty();
        page.Picker.Rows.ShouldHaveSingleItem();

        source.SaveFailure = null;
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        page.SaveError.ShouldBeEmpty();
        page.HasError.ShouldBeFalse();
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A save that worked stays a success when the list cannot be read afterwards; the announcement says why the list is stale.</summary>
    [Fact]
    public async Task ASavedSetSurvivesAListThatCannotBeRefreshed()
    {
        var (page, source) = await OpenAsync(s => s.ListFailsAfterSave = true).ConfigureAwait(true);
        page.Picker.AddFolder("/work/api");

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        source.SavedNames.ShouldHaveSingleItem();
        page.IsDirty.ShouldBeFalse();
        page.State.ShouldBe(SettingsPageState.Ready);
        page.Announcement.ShouldContain("The list is gone.", Case.Sensitive);
        page.Announcement.ShouldStartWith(WorkspacesCopy.Saved("Workspace"), Case.Sensitive);
    }

    /// <summary>Reset clears the picker to one empty set named Workspace; it is dirty only when that differs from the saved set.</summary>
    [Fact]
    public async Task ResetClearsThePicker()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, _) = await OpenAsync().ConfigureAwait(true);
        page.Picker.AddFolder("/work/api");
        page.Picker.Name = "Backend";

        page.Reset();

        page.Picker.Rows.ShouldBeEmpty();
        page.Picker.Name.ShouldBe(WorkspacesCopy.DefaultName);
        page.IsDirty.ShouldBeFalse("the empty set is what the page loaded with");

        page.Picker.AddFolder("/work/api");
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        page.Reset();

        page.Picker.Rows.ShouldBeEmpty();
        page.IsDirty.ShouldBeTrue("the saved set has a folder, the reset set has none");
    }

    /// <summary>Reset drops an earlier save error.</summary>
    [Fact]
    public async Task ResetClearsTheSaveError()
    {
        var (page, _) = await OpenAsync(s => s.SaveFailure = "Nope.").ConfigureAwait(true);
        page.Picker.AddFolder("/work/api");
        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        page.Reset();

        page.SaveError.ShouldBeEmpty();
    }

    /// <summary>Discard returns the picker to the last saved set, including its name and primary, and drops the save error.</summary>
    [Fact]
    public async Task DiscardReturnsToTheSavedSet()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source) = await OpenAsync().ConfigureAwait(true);
        page.Picker.AddFolder("/work/api");
        page.Picker.AddFolder("/work/lib");
        page.Picker.Name = "Backend";
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        page.Picker.MakePrimary("lib").ShouldBeTrue();
        page.Picker.Remove("api").ShouldBeTrue();
        page.Picker.Name = "Other";
        source.SaveFailure = "Nope.";
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();
        page.IsDirty.ShouldBeTrue();

        page.Discard();

        page.Picker.Name.ShouldBe("Backend");
        page.Picker.Rows.Select(row => (row.Id, row.IsPrimary)).ShouldBe([("api", true), ("lib", false)]);
        page.IsDirty.ShouldBeFalse();
        page.SaveError.ShouldBeEmpty();
    }

    /// <summary>Loading a recent set puts its name and folders into the editor as a change that is not saved yet, and announces it.</summary>
    [Fact]
    public async Task ARecentSetLoadsIntoTheEditor()
    {
        var (page, source) = await OpenAsync(s => s.Recents.Add(ScriptedWorkspacesSource.Row("Backend", "/work/api", "/work/lib"))).ConfigureAwait(true);

        page.LoadRecentIntoEditor("backend").ShouldBeTrue();

        page.Picker.Name.ShouldBe("Backend");
        page.Picker.Snapshot().Select(root => (root.Path, root.IsPrimary)).ShouldBe([("/work/api", true), ("/work/lib", false)]);
        page.IsDirty.ShouldBeTrue();
        page.Announcement.ShouldBe(WorkspacesCopy.Loaded("Backend"));
        source.SavedNames.ShouldBeEmpty("loading into the editor saves nothing");

        page.LoadRecentIntoEditor("missing").ShouldBeFalse();
        page.Picker.Name.ShouldBe("Backend");
    }

    /// <summary>A recent set whose stored folders repeat an identifier is not loaded: the editor keeps its folders and the page says what to do.</summary>
    [Fact]
    public async Task AnUnusableRecentSetIsNotLoaded()
    {
        var broken = new WorkspaceRecentRow(
            "broken",
            "Broken",
            2,
            "/work/a",
            "2026-10-28 09:00 UTC",
            [new WorkspacePickerRoot("a", "/work/a", "a", null, true), new WorkspacePickerRoot("a", "/work/b", "b", null, false)]);
        var (page, _) = await OpenAsync(s => s.Recents.Add(broken)).ConfigureAwait(true);
        page.Picker.AddFolder("/work/api");

        page.LoadRecentIntoEditor("broken").ShouldBeFalse();

        page.Picker.Rows.Select(row => row.Id).ShouldBe(["api"]);
        page.Announcement.ShouldBe(WorkspacesCopy.RecentUnusable("Broken"));
    }

    /// <summary>Forgetting a recent set removes it from the source and the list, announces it and leaves the editor alone.</summary>
    [Fact]
    public async Task ForgettingRemovesTheSet()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source) = await OpenAsync(s =>
        {
            s.Recents.Add(ScriptedWorkspacesSource.Row("Backend", "/work/api"));
            s.Recents.Add(ScriptedWorkspacesSource.Row("Docs", "/work/docs"));
        }).ConfigureAwait(true);
        page.Picker.AddFolder("/work/scratch");

        var removed = await page.ForgetRecentAsync("backend", token).ConfigureAwait(true);

        removed.ShouldBeTrue();
        page.Recents.Select(row => row.Name).ShouldBe(["Docs"]);
        source.Recents.Select(row => row.Name).ShouldBe(["Docs"]);
        page.Announcement.ShouldBe(WorkspacesCopy.Forgot("Backend"));
        page.Picker.Rows.ShouldHaveSingleItem();

        (await page.ForgetRecentAsync("backend", token).ConfigureAwait(true)).ShouldBeFalse("it is no longer listed");
        (await page.ForgetRecentAsync("docs", token).ConfigureAwait(true)).ShouldBeTrue();

        page.HasNoRecents.ShouldBeTrue();
    }

    /// <summary>A failed forget keeps the set in the list and announces the reason.</summary>
    [Fact]
    public async Task AFailedForgetKeepsTheSetListed()
    {
        var (page, _) = await OpenAsync(s =>
        {
            s.Recents.Add(ScriptedWorkspacesSource.Row("Backend", "/work/api"));
            s.ForgetFailure = "The file is read-only.";
        }).ConfigureAwait(true);

        var removed = await page.ForgetRecentAsync("backend", TestContext.Current.CancellationToken).ConfigureAwait(true);

        removed.ShouldBeFalse();
        page.Recents.ShouldHaveSingleItem();
        page.Announcement.ShouldBe("The file is read-only.");
    }

    /// <summary>A set another process already forgot leaves the list and the page says it was gone.</summary>
    [Fact]
    public async Task ASetThatWasAlreadyGoneLeavesTheList()
    {
        var (page, source) = await OpenAsync(s => s.Recents.Add(ScriptedWorkspacesSource.Row("Backend", "/work/api"))).ConfigureAwait(true);
        source.Recents.Clear();

        var removed = await page.ForgetRecentAsync("backend", TestContext.Current.CancellationToken).ConfigureAwait(true);

        removed.ShouldBeFalse();
        page.Recents.ShouldBeEmpty();
        page.Announcement.ShouldBe(WorkspacesCopy.AlreadyForgotten("Backend"));
    }

    /// <summary>The picker's import and export work through the page's source backend; an imported file is a draft that is saved like any other.</summary>
    [Fact]
    public async Task AnImportedFileIsADraft()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source) = await OpenAsync(s => s.Backend.Import = new WorkspacePickerImport(
            "Mono",
            [new WorkspacePickerRoot("api", "/work/api", "api", null, true), new WorkspacePickerRoot("web", "/work/web", "web", null, false)],
            ["A remote folder was skipped."])).ConfigureAwait(true);

        page.Picker.ImportCodeWorkspace("/work/mono.code-workspace").ShouldBeTrue();

        page.Picker.Name.ShouldBe("Mono");
        page.IsDirty.ShouldBeTrue();
        page.Picker.ImportWarnings.ShouldBe(["A remote folder was skipped."]);

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        page.Picker.ExportCodeWorkspace("/work/out.code-workspace").ShouldBeTrue();

        source.SavedNames.ShouldBe(["Mono"]);
        source.Backend.Exported.ShouldBe(["/work/out.code-workspace"]);
    }

    /// <summary>A row says its folder count in the singular and plural, and its summary carries name, count, primary and time.</summary>
    [Fact]
    public void ARecentRowSpeaksForItself()
    {
        var one = ScriptedWorkspacesSource.Row("Docs", "/work/docs");
        var two = ScriptedWorkspacesSource.Row("Backend", "/work/api", "/work/lib");

        one.FoldersText.ShouldBe("1 folder");
        two.FoldersText.ShouldBe("2 folders");
        two.Summary.ShouldBe("Backend, 2 folders, primary /work/api. Last used 2026-10-28 09:00 UTC.");
        two.LoadName.ShouldContain("Backend", Case.Sensitive);
        two.ForgetName.ShouldContain("Backend", Case.Sensitive);
        string.Equals(two.LoadName, two.ForgetName, StringComparison.Ordinal).ShouldBeFalse();
    }
}
