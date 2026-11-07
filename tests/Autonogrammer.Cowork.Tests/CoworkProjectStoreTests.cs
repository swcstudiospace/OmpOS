using Autonogrammer.Cowork.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests;

/// <summary>Project files follow the settings-file rules.</summary>
public sealed class CoworkProjectStoreTests
{
    [Fact]
    public void WritesAreAtomicOwnerOnlyAndKeepUnknownMembers()
    {
        using var directory = new TempState();
        var key = CoworkAccountFolder.KeyFromSubject("subject-projects");
        var store = new CoworkProjectStore(directory.Path, key);
        var account = Path.GetDirectoryName(store.FilePath)!;
        Directory.CreateDirectory(account);
        File.WriteAllText(store.FilePath, """{"schemaVersion":1,"later":1,"projects":[]}""");

        store.Upsert(new CoworkProject
        {
            Id = "p-1",
            Name = "Desk",
            Folder = "/desk",
            Instructions = "Keep it short.",
        });

        Directory.GetFiles(account, "*.tmp").ShouldBeEmpty();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(store.FilePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        File.ReadAllText(store.FilePath).ShouldContain("\"later\":1");
        var project = store.List().ShouldHaveSingleItem();
        project.Id.ShouldBe("p-1");
        project.Name.ShouldBe("Desk");
        project.Folder.ShouldBe("/desk");
        project.Instructions.ShouldBe("Keep it short.");
        store.FilePath.ShouldEndWith(Path.Combine("cowork", key, "projects.json"));
    }

    [Fact]
    public void AnOversizedFileIsRefusedAndNotOverwritten()
    {
        using var directory = new TempState();
        var store = new CoworkProjectStore(directory.Path, CoworkAccountFolder.KeyFromSubject("subject-projects-cap"));
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        var oversized = new string('y', CoworkProjectStore.MaxFileBytes + 1);
        File.WriteAllText(store.FilePath, oversized);

        Should.Throw<InvalidDataException>(() => store.Upsert(new CoworkProject { Id = "p-1", Name = "n" }));
        File.ReadAllText(store.FilePath).ShouldBe(oversized);
    }

    private sealed class TempState : IDisposable
    {
        public TempState()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cowork-proj-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
