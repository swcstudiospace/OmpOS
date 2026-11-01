using System.Text.Json;
using Autonogrammer.Cowork.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Sessions;

/// <summary>The global grant file is owner-only and atomic, and a project grant survives a new session of the same project.</summary>
public sealed class CoworkFolderGrantStoreTests
{
    [Fact]
    public void GlobalStoreIsOwnerOnlyAtomicAndKeepsUnknownKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), "cowork-grants-" + Guid.NewGuid().ToString("N"));
        var key = CoworkAccountFolder.KeyFromSubject("subject-one");
        Directory.CreateDirectory(CoworkAccountFolder.AccountDirectory(root, key));
        var store = new CoworkFolderGrantStore(root, key);
        File.WriteAllText(store.FilePath, """{"schemaVersion":1,"customNote":"keep","grants":[]}""");
        var notice = store.Grant(new CoworkFolderGrant("/tmp/work", CoworkFolderAccess.ReadWrite, CoworkFolderScope.Global));
        notice.ShouldBe(CoworkFolderGrantStore.FolderChangeAppliesNextSession);
        store.Revoke("/tmp/work").ShouldBe(CoworkFolderGrantStore.FolderChangeAppliesNextSession);
        var json = File.ReadAllText(store.FilePath);
        json.ShouldContain("customNote");
        json.ShouldContain("keep");
        Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!).ShouldNotContain(path => Path.GetFileName(path).Contains('.', StringComparison.Ordinal) && Path.GetFileName(path).Length > "folder-grants.json".Length && path.Contains(Guid.Empty.ToString(), StringComparison.Ordinal));
        if (OperatingSystem.IsLinux())
        {
            File.GetUnixFileMode(store.FilePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void AProjectScopeGrantSurvivesANewSessionOfTheSameProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "cowork-project-grants-" + Guid.NewGuid().ToString("N"));
        var key = CoworkAccountFolder.KeyFromSubject("subject-one");
        var store = new CoworkProjectStore(root, key);
        store.Upsert(new CoworkProject { Id = "proj", Name = "Proj", Folder = "/tmp/proj" });
        var notice = store.SetFolderGrants("proj", [new CoworkFolderGrant("/tmp/proj", CoworkFolderAccess.ReadOnly, CoworkFolderScope.Project)]);
        notice.ShouldBe(CoworkProjectStore.FolderChangeAppliesNextSession);
        var again = new CoworkProjectStore(root, key);
        var grants = again.ListFolderGrants("proj");
        grants.Count.ShouldBe(1);
        grants[0].Access.ShouldBe(CoworkFolderAccess.ReadOnly);
        grants[0].Scope.ShouldBe(CoworkFolderScope.Project);
        using var document = JsonDocument.Parse(File.ReadAllText(again.FilePath));
        document.RootElement.GetProperty("projects")[0].TryGetProperty("folderGrants", out _).ShouldBeTrue();
        Directory.Delete(root, recursive: true);
    }
}
