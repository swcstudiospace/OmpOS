using System.Globalization;
using System.Text.Json;
using Autonogrammer.Cowork.Projects;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Projects;

/// <summary>An old projects.json loads, unknown members stay, and folder becomes folders[0].</summary>
public sealed class ProjectStoreMigrationTests
{
    [Fact]
    public void OldProjectsJsonKeepsUnknownMembersAndMapsFolder()
    {
        using var directory = new TempState();
        var key = global::Autonogrammer.Cowork.CoworkAccountFolder.KeyFromSubject("migration");
        var store = new CoworkProjectStore(directory.Path, key);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        File.Copy(FindFixture(), store.FilePath);

        var project = store.List().ShouldHaveSingleItem();
        project.Id.ShouldBe("old-1");
        project.Name.ShouldBe("Legacy");
        project.Instructions.ShouldBe("keep the old note");
        project.Folders.ShouldNotBeEmpty();
        project.Folders[0].ShouldBe("/tmp/legacy-desk");
        project.UseAccountMemory.ShouldBeFalse();

        store.Upsert(project with { Description = "kept" });
        var json = File.ReadAllText(store.FilePath);
        json.Contains("\"later\":true", StringComparison.Ordinal).ShouldBeTrue();
        json.Contains("\"mystery\":7", StringComparison.Ordinal).ShouldBeTrue();
        json.Contains("\"folder\":\"/tmp/legacy-desk\"", StringComparison.Ordinal).ShouldBeTrue();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("projects")[0].GetProperty("folders")[0].GetString().ShouldBe("/tmp/legacy-desk");
    }

    [Fact]
    public void CorruptFileIsMovedAsideAndReturnsEmpty()
    {
        using var directory = new TempState();
        var store = new CoworkProjectStore(directory.Path, global::Autonogrammer.Cowork.CoworkAccountFolder.KeyFromSubject("corrupt"));
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        File.WriteAllText(store.FilePath, "{not-json");

        var projects = store.List();
        projects.ShouldBeEmpty();
        store.LastNotice.ShouldBe(CoworkProjectStore.ProjectFileCorrupt);
        store.MovedAsidePath.ShouldNotBeNull();
        File.Exists(store.FilePath).ShouldBeFalse();
        File.Exists(store.MovedAsidePath).ShouldBeTrue();
    }

    [Fact]
    public void HardDeleteRequiresBothCountsAndNeverTouchesFolders()
    {
        using var directory = new TempState();
        var folder = Path.Combine(directory.Path, "bound");
        Directory.CreateDirectory(folder);
        var store = new CoworkProjectStore(directory.Path, global::Autonogrammer.Cowork.CoworkAccountFolder.KeyFromSubject("delete"));
        store.Upsert(new CoworkProject
        {
            Id = "p-1",
            Name = "Desk",
            Folders = [folder],
        });

        store.TryHardDelete("p-1", new ProjectDeleteConfirmation(0, 3), memoryCount: 3).ShouldBeFalse();
        store.List(includeArchived: true).ShouldNotBeEmpty();
        store.TryHardDelete("p-1", new ProjectDeleteConfirmation(1, 3), memoryCount: 3).ShouldBeTrue();
        store.List(includeArchived: true).ShouldBeEmpty();
        Directory.Exists(folder).ShouldBeTrue();
    }

    [Fact]
    public void MissingFolderIsANoticeNotAnException()
    {
        var workspace = CoworkProjectStore.Workspace(new CoworkProject { Id = "p", Name = "Desk" });
        workspace.Notice.ShouldBe(CoworkProjectStore.FolderMissing);
        workspace.Folder.ShouldBeNull();
    }

    [Fact]
    public void ArchiveHidesTheProject()
    {
        using var directory = new TempState();
        var store = new CoworkProjectStore(directory.Path, global::Autonogrammer.Cowork.CoworkAccountFolder.KeyFromSubject("archive"));
        store.Upsert(new CoworkProject { Id = "p-1", Name = "Desk" });
        store.Archive("p-1", DateTimeOffset.Parse("2026-01-02T00:00:00Z", CultureInfo.InvariantCulture));
        store.List().ShouldBeEmpty();
        store.List(includeArchived: true).ShouldHaveSingleItem().ArchivedAt.ShouldNotBeNull();
    }

    private static string FindFixture()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "Autonogrammer.Cowork.Tests", "Projects", "Fixtures", "old-projects.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("old-projects.json");
    }

    private sealed class TempState : IDisposable
    {
        public TempState()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cowork-mig-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
