using OmpOS.Native.Settings;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Workspaces;
using OmpOS.Workspace.Sets;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>
/// The Workspaces page's head source over a real store and real directories: recents saved, listed and forgotten, the folder checks of the
/// validator, and <c>.code-workspace</c> files read and written. Only the clock is fixed.
/// </summary>
public sealed class WorkspacesSettingsSourceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 28, 12, 30, 0, TimeSpan.Zero);

    private static string Folder(HeadTempDirectory temp, string name, bool git = true)
    {
        var path = temp.PathOf(name);
        _ = Directory.CreateDirectory(git ? Path.Combine(path, ".git") : path);
        return path;
    }

    private static WorkspacePickerRoot Root(string id, string path, bool primary = false) => new(id, path, id, null, primary);

    private static WorkspaceSetStore Store(HeadTempDirectory temp) => new(temp.PathOf("config/workspace-sets.json"), new FixedClock(Noon));

    /// <summary>Saving, listing and forgetting go through the real store: the row carries name, count, primary path, a stable time text and the roots, and saving a name again replaces the set.</summary>
    [Fact]
    public async Task SetsAreSavedListedAndForgotten()
    {
        var token = TestContext.Current.CancellationToken;
        using var temp = new HeadTempDirectory();
        using var store = Store(temp);
        var source = new WorkspacesSettingsSource(store);
        var api = Folder(temp, "api");
        var web = Folder(temp, "web");
        var docs = Folder(temp, "docs");

        await source.SaveAsync("Team A", [Root("api", api, primary: true), Root("web", web)], token).ConfigureAwait(true);
        await source.SaveAsync("Docs", [Root("docs", docs, primary: true)], token).ConfigureAwait(true);
        var listed = await source.ListRecentsAsync(token).ConfigureAwait(true);

        listed.Select(row => row.Name).ShouldBe(["Docs", "Team A"]);
        var team = listed[1];
        team.Id.ShouldBe("team-a");
        team.FolderCount.ShouldBe(2);
        team.PrimaryPath.ShouldBe(api);
        team.LastUsedText.ShouldBe("2026-10-28 12:30 UTC");
        team.Roots.Select(root => (root.Id, root.Path, root.IsPrimary)).ShouldBe([("api", api, true), ("web", web, false)]);

        await source.SaveAsync(" team   a ", [Root("web", web, primary: true)], token).ConfigureAwait(true);
        var replaced = await source.ListRecentsAsync(token).ConfigureAwait(true);

        replaced.Select(row => row.Id).ShouldBe(["team-a", "docs"], "the same name replaces the set and moves it to the front");
        replaced[0].Name.ShouldBe("team   a");
        replaced[0].FolderCount.ShouldBe(1);

        (await source.ForgetAsync("team-a", token).ConfigureAwait(true)).ShouldBeTrue();
        (await source.ForgetAsync("team-a", token).ConfigureAwait(true)).ShouldBeFalse();
        (await source.ListRecentsAsync(token).ConfigureAwait(true)).Select(row => row.Id).ShouldBe(["docs"]);
    }

    /// <summary>The sets are in the store the slash commands read: a second store over the same file sees what the source saved.</summary>
    [Fact]
    public async Task TheSavedSetIsInTheSharedStore()
    {
        var token = TestContext.Current.CancellationToken;
        using var temp = new HeadTempDirectory();
        var api = Folder(temp, "api");
        using (var store = Store(temp))
        {
            await new WorkspacesSettingsSource(store).SaveAsync("Backend", [Root("api", api, primary: true)], token).ConfigureAwait(true);
        }

        using var other = Store(temp);
        var recents = await other.ListAsync(token).ConfigureAwait(true);

        var recent = recents.ShouldHaveSingleItem();
        recent.Set.Name.ShouldBe("Backend");
        recent.Set.Id.ShouldBe(WorkspacesSettingsSource.SetIdFor("Backend"));
        recent.Set.PrimaryPath.ShouldBe(api);
    }

    /// <summary>Names that are not plain ASCII keep their own identity instead of collapsing to one identifier.</summary>
    [Theory]
    [InlineData("Team A", "team-a")]
    [InlineData("  team_a  ", "team-a")]
    [InlineData("Backend", "backend")]
    [InlineData("Проект", "проект")]
    [InlineData("a/b", "a/b")]
    [InlineData("a?b", "a?b")]
    [InlineData("---", "---")]
    public void TheIdentifierFollowsTheName(string name, string expected) =>
        WorkspacesSettingsSource.SetIdFor(name).ShouldBe(expected);

    /// <summary>A name or set the commands could not use is refused with a sentence, and nothing is written.</summary>
    [Fact]
    public async Task UnusableSetsAreRefused()
    {
        var token = TestContext.Current.CancellationToken;
        using var temp = new HeadTempDirectory();
        using var store = Store(temp);
        var source = new WorkspacesSettingsSource(store);
        var api = Folder(temp, "api");
        WorkspacePickerRoot[] one = [Root("api", api, primary: true)];

        (await Should.ThrowAsync<SettingsSourceException>(() => source.SaveAsync("  ", one, token)).ConfigureAwait(true)).Message.ShouldBe(WorkspacesSourceCopy.NameBlank);
        (await Should.ThrowAsync<SettingsSourceException>(() => source.SaveAsync(new string('n', 81), one, token)).ConfigureAwait(true)).Message.ShouldBe(WorkspacesSourceCopy.NameUnusable);
        (await Should.ThrowAsync<SettingsSourceException>(() => source.SaveAsync("a\nb", one, token)).ConfigureAwait(true)).Message.ShouldBe(WorkspacesSourceCopy.NameUnusable);
        (await Should.ThrowAsync<SettingsSourceException>(() => source.SaveAsync("Empty", [], token)).ConfigureAwait(true)).Message.ShouldBe(WorkspacesSourceCopy.NothingToSave);

        (await store.ListAsync(token).ConfigureAwait(true)).ShouldBeEmpty();
        File.Exists(temp.PathOf("config/workspace-sets.json")).ShouldBeFalse();
    }

    /// <summary>A store file that cannot be written (its folder is a file) is the expected failure of a save and a forget, not a crash.</summary>
    [Fact]
    public async Task AStoreThatCannotBeWrittenFailsWithASentence()
    {
        var token = TestContext.Current.CancellationToken;
        using var temp = new HeadTempDirectory();
        var blocker = temp.Write("blocker", "this is a file, not a folder");
        using var store = new WorkspaceSetStore(Path.Combine(blocker, "sets.json"), new FixedClock(Noon));
        var source = new WorkspacesSettingsSource(store);
        var api = Folder(temp, "api");

        var save = await Should.ThrowAsync<SettingsSourceException>(() => source.SaveAsync("Backend", [Root("api", api, primary: true)], token)).ConfigureAwait(true);

        save.Message.ShouldStartWith("The workspace set could not be saved:", Case.Sensitive);
        (await source.ListRecentsAsync(token).ConfigureAwait(true)).ShouldBeEmpty("an unreadable file lists nothing");
        (await source.ForgetAsync("backend", token).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>A missing folder is an error on its own row, a folder that is not a git root only a warning, a clean set has no finding, and no folder has none.</summary>
    [Fact]
    public void TheBackendJudgesFoldersOnTheRealFileSystem()
    {
        using var temp = new HeadTempDirectory();
        var backend = new WorkspaceSetPickerBackend();
        var api = Folder(temp, "api");
        var plain = Folder(temp, "plain", git: false);
        var ghost = temp.PathOf("ghost");

        backend.Validate([]).ShouldBeEmpty();
        backend.Validate([Root("api", api, primary: true)]).ShouldBeEmpty();

        var plainIssue = backend.Validate([Root("api", api, primary: true), Root("plain", plain)]).ShouldHaveSingleItem();
        plainIssue.IsError.ShouldBeFalse();
        plainIssue.RootId.ShouldBe("plain");

        var missing = backend.Validate([Root("api", api, primary: true), Root("ghost", ghost)]).ShouldHaveSingleItem();
        missing.IsError.ShouldBeTrue();
        missing.RootId.ShouldBe("ghost");
        missing.Message.ShouldContain("does not exist", Case.Sensitive);
    }

    /// <summary>One folder inside another, the same folder twice and a relative path are errors on the row that causes them.</summary>
    [Fact]
    public void TheBackendRejectsNestedDuplicateAndRelativeFolders()
    {
        using var temp = new HeadTempDirectory();
        var backend = new WorkspaceSetPickerBackend();
        var api = Folder(temp, "api");
        var inner = Folder(temp, "api/inner");

        var nested = backend.Validate([Root("api", api, primary: true), Root("inner", inner)]);
        nested.ShouldContain(issue => issue.IsError && string.Equals(issue.RootId, "inner", StringComparison.Ordinal) && issue.Message.Contains("inside", StringComparison.Ordinal));

        var twice = backend.Validate([Root("api", api, primary: true), Root("again", api + Path.DirectorySeparatorChar)]);
        twice.ShouldContain(issue => issue.IsError && string.Equals(issue.RootId, "again", StringComparison.Ordinal) && issue.Message.Contains("same folder", StringComparison.Ordinal));

        var relative = backend.Validate([Root("rel", "relative/folder", primary: true)]);
        relative.ShouldContain(issue => issue.IsError && string.Equals(issue.RootId, "rel", StringComparison.Ordinal) && issue.Message.Contains("absolute", StringComparison.Ordinal));
    }

    /// <summary>A workspace file with comments and trailing commas is read into rows: names from <c>name</c> or the last segment, the first folder primary, paths absolute.</summary>
    [Fact]
    public void ImportReadsAWorkspaceFile()
    {
        using var temp = new HeadTempDirectory();
        var api = Folder(temp, "api");
        var web = Folder(temp, "web");
        var file = temp.Write(
            "team.code-workspace",
            "{\n\t// the team workspace\n\t\"folders\": [\n\t\t{ \"path\": \"api\" },\n\t\t{ \"path\": \"web\", \"name\": \"Frontend\", },\n\t],\n\t\"settings\": { \"editor.tabSize\": 2 }\n}\n");
        var backend = new WorkspaceSetPickerBackend();

        var import = backend.ImportCodeWorkspace(file);

        import.Name.ShouldBe("team");
        import.Warnings.ShouldBeEmpty();
        import.Roots.Select(root => (root.Path, root.DisplayName, root.IsPrimary)).ShouldBe([(api, "api", true), (web, "Frontend", false)]);
    }

    /// <summary>A missing file, a file that is not JSON and a file whose top level is not an object fail with the exceptions the picker turns into a sentence.</summary>
    [Fact]
    public void ImportFailuresAreTheExpectedKinds()
    {
        using var temp = new HeadTempDirectory();
        var backend = new WorkspaceSetPickerBackend();

        Should.Throw<FileNotFoundException>(() => backend.ImportCodeWorkspace(temp.PathOf("missing.code-workspace")));
        Should.Throw<InvalidDataException>(() => backend.ImportCodeWorkspace(temp.Write("bad.code-workspace", "{ not json")));
        Should.Throw<InvalidDataException>(() => backend.ImportCodeWorkspace(temp.Write("array.code-workspace", "[1, 2]")));
        Should.Throw<InvalidDataException>(() => backend.ImportCodeWorkspace(temp.Write("empty.code-workspace", "{ \"folders\": [] }")));
    }

    /// <summary>Exporting into a file that exists keeps everything it holds besides its folders; exporting to a new path creates the file and an import gives the same rows back.</summary>
    [Fact]
    public void ExportKeepsWhatTheFileHoldsAndRoundTrips()
    {
        using var temp = new HeadTempDirectory();
        var api = Folder(temp, "api");
        var web = Folder(temp, "web");
        var docs = Folder(temp, "docs");
        var existing = temp.Write(
            "team.code-workspace",
            "{\n\t\"folders\": [ { \"path\": \"api\" } ],\n\t\"settings\": { \"editor.tabSize\": 2 }\n}\n");
        var backend = new WorkspaceSetPickerBackend();
        WorkspacePickerRoot[] roots = [Root("api", api, primary: true), Root("web", web), Root("docs", docs)];

        backend.ExportCodeWorkspace(existing, roots);

        File.ReadAllText(existing).ShouldContain("\"editor.tabSize\"", Case.Sensitive);
        backend.ImportCodeWorkspace(existing).Roots.Select(root => root.Path).ShouldBe([api, web, docs]);

        var created = temp.PathOf("out/new.code-workspace");
        backend.ExportCodeWorkspace(created, roots);
        var back = backend.ImportCodeWorkspace(created);

        File.Exists(created).ShouldBeTrue();
        back.Name.ShouldBe("new");
        back.Roots.Select(root => (root.Path, root.IsPrimary)).ShouldBe([(api, true), (web, false), (docs, false)]);
    }

    /// <summary>A remote folder and an empty set cannot be exported, and nothing is written for them.</summary>
    [Fact]
    public void ExportRefusesRemoteFoldersAndEmptySets()
    {
        using var temp = new HeadTempDirectory();
        var backend = new WorkspaceSetPickerBackend();
        var target = temp.PathOf("remote.code-workspace");

        var remote = Should.Throw<InvalidOperationException>(() => backend.ExportCodeWorkspace(target, [new WorkspacePickerRoot("app", "/srv/app", "app", "ssh-dev", IsPrimary: true)]));
        var empty = Should.Throw<InvalidOperationException>(() => backend.ExportCodeWorkspace(target, []));

        remote.Message.ShouldContain("ssh-dev", Case.Sensitive);
        empty.Message.ShouldBe(WorkspacesSourceCopy.NothingToExport);
        File.Exists(target).ShouldBeFalse();
    }

    /// <summary>The whole page over the real source: add real folders, save, find the set in the store, load it back after a reset, and forget it.</summary>
    [Fact]
    public async Task ThePageWorksEndToEndOverTheRealSource()
    {
        var token = TestContext.Current.CancellationToken;
        using var temp = new HeadTempDirectory();
        using var store = Store(temp);
        var api = Folder(temp, "api");
        var web = Folder(temp, "web");
        var page = new WorkspacesSettingsPage(new WorkspacesSettingsSource(store));
        await page.LoadAsync(token).ConfigureAwait(true);

        page.Picker.AddFolder(api);
        page.Picker.AddFolder(Path.Combine(temp.Root, "ghost"));
        page.Picker.Name = "Team";

        page.Picker.CanStart.ShouldBeFalse();
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();
        page.SaveError.ShouldContain("does not exist", Case.Sensitive);
        (await store.ListAsync(token).ConfigureAwait(true)).ShouldBeEmpty();

        page.Picker.Remove("ghost").ShouldBeTrue();
        page.Picker.AddFolder(web);
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        var recent = page.Recents.ShouldHaveSingleItem();
        recent.Name.ShouldBe("Team");
        recent.FolderCount.ShouldBe(2);
        (await store.ListAsync(token).ConfigureAwait(true)).ShouldHaveSingleItem().Set.Roots.Select(root => root.Path).ShouldBe([api, web]);

        page.Reset();
        page.Picker.Rows.ShouldBeEmpty();
        page.LoadRecentIntoEditor(recent.Id).ShouldBeTrue();
        page.Picker.Snapshot().Select(root => root.Path).ShouldBe([api, web]);
        page.IsDirty.ShouldBeFalse("the loaded set is the saved set");

        (await page.ForgetRecentAsync(recent.Id, token).ConfigureAwait(true)).ShouldBeTrue();
        (await store.ListAsync(token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
