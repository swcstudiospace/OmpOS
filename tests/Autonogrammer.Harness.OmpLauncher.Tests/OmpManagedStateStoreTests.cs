using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpLauncher.Tests;

public sealed class OmpManagedStateStoreTests : IDisposable
{
    private readonly string _directory;

    public OmpManagedStateStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "autonogrammer-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void RoundTripsEveryField()
    {
        var store = new OmpManagedStateStore(Path.Combine(_directory, "state.json"));
        var checkedAt = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        var quarantined = new OmpQuarantinedVersion("2.0.0", "boom", checkedAt);

        store.Save(new OmpManagedState("1.0.0", "ABCD", "0.9.0", [quarantined], checkedAt));
        var loaded = store.Load();

        loaded.ActiveVersion.ShouldBe("1.0.0");
        loaded.ActiveSha256.ShouldBe("ABCD");
        loaded.PreviousGood.ShouldBe("0.9.0");
        loaded.LastCheckedAt.ShouldBe(checkedAt);
        loaded.IsQuarantined("2.0.0").ShouldBeTrue();
        loaded.Quarantined!.Single().Reason.ShouldBe("boom");
    }

    [Fact]
    public void MissingFileLoadsAsEmpty()
    {
        var store = new OmpManagedStateStore(Path.Combine(_directory, "absent.json"));

        store.Load().ActiveVersion.ShouldBeNull();
    }

    [Fact]
    public void SaveLeavesNoTemporaryFileBehind()
    {
        var store = new OmpManagedStateStore(Path.Combine(_directory, "state.json"));

        store.Save(OmpManagedState.Empty);
        store.Save(new OmpManagedState("1.0.0", "ABCD", null, null, null));

        Directory.GetFiles(_directory, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public void CorruptFileIsInvalidData()
    {
        var path = Path.Combine(_directory, "state.json");
        File.WriteAllText(path, "{not json");
        var store = new OmpManagedStateStore(path);

        Should.Throw<InvalidDataException>(() => store.Load());
    }
}
