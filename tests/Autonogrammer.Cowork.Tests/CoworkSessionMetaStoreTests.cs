using Autonogrammer.Cowork.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests;

/// <summary>Session metadata follows the settings-file rules.</summary>
public sealed class CoworkSessionMetaStoreTests
{
    [Fact]
    public void WritesAreAtomicOwnerOnlyAndKeepUnknownMembers()
    {
        using var directory = new TempState();
        var key = CoworkAccountFolder.KeyFromSubject("subject-meta");
        var store = new CoworkSessionMetaStore(directory.Path, key);
        var account = Path.GetDirectoryName(store.FilePath)!;
        Directory.CreateDirectory(account);
        File.WriteAllText(store.FilePath, """{"schemaVersion":1,"future":true,"sessions":{}}""");

        store.Upsert("s-1", new CoworkSessionMeta
        {
            ProjectId = "p-1",
            TrustedFolders = ["/work"],
            MemoryOff = true,
            PinnedArtifactIds = ["a-1"],
        });

        Directory.GetFiles(account, "*.tmp").ShouldBeEmpty();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(store.FilePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        var json = File.ReadAllText(store.FilePath);
        json.ShouldContain("\"future\":true");
        var read = store.Get("s-1");
        read.ShouldNotBeNull();
        read.ProjectId.ShouldBe("p-1");
        read.MemoryOff.ShouldBeTrue();
        read.TrustedFolders.ShouldBe(["/work"]);
        read.PinnedArtifactIds.ShouldBe(["a-1"]);
    }

    [Fact]
    public void AnOversizedFileIsRefusedAndNotOverwritten()
    {
        using var directory = new TempState();
        var key = CoworkAccountFolder.KeyFromSubject("subject-meta-cap");
        var store = new CoworkSessionMetaStore(directory.Path, key);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        var oversized = new string('x', CoworkSessionMetaStore.MaxFileBytes + 1);
        File.WriteAllText(store.FilePath, oversized);

        Should.Throw<InvalidDataException>(() => store.Upsert("s-1", new CoworkSessionMeta()));
        File.ReadAllText(store.FilePath).ShouldBe(oversized);
    }

    private sealed class TempState : IDisposable
    {
        public TempState()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cowork-meta-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
