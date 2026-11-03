using Shouldly;
using Xunit;

namespace Autonogrammer.Dispatch.Desktop.Tests;

public sealed class DedupeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dispatch-dedupe-" + Guid.NewGuid().ToString("N"));

    private string StatePath => Path.Combine(_directory, "dedupe.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void ANewCommandExecutesOnceAndARedeliveryIsADuplicate()
    {
        var store = new DispatchDedupeStore(StatePath);

        store.Admit("cmd-1", "n-1", 1).ShouldBe(DispatchAdmission.Execute);
        store.Admit("cmd-1", "n-1", 1).ShouldBe(DispatchAdmission.Duplicate);
        store.HighWater.ShouldBe(1);
    }

    [Fact]
    public void ARestartNeverRunsACommandAlreadyReceived()
    {
        var first = new DispatchDedupeStore(StatePath);
        first.Admit("cmd-1", "n-1", 1).ShouldBe(DispatchAdmission.Execute);
        first.Admit("cmd-2", "n-2", 2).ShouldBe(DispatchAdmission.Execute);

        var restarted = new DispatchDedupeStore(StatePath);

        restarted.HighWater.ShouldBe(2);
        restarted.Admit("cmd-1", "n-1", 1).ShouldBe(DispatchAdmission.Duplicate);
        restarted.Admit("cmd-2", "n-2", 2).ShouldBe(DispatchAdmission.Duplicate);
        restarted.Admit("cmd-3", "n-3", 3).ShouldBe(DispatchAdmission.Execute);
    }

    [Fact]
    public void AGapInTheSequenceDoesNotStallDelivery()
    {
        var store = new DispatchDedupeStore(StatePath);

        store.Admit("cmd-1", "n-1", 1).ShouldBe(DispatchAdmission.Execute);
        store.Admit("cmd-5", "n-5", 5).ShouldBe(DispatchAdmission.Execute);

        // The skipped sequence arrives late: it is at or below the high-water mark, so it is never run.
        store.Admit("cmd-3", "n-3", 3).ShouldBe(DispatchAdmission.Duplicate);
        store.HighWater.ShouldBe(5);
    }

    [Fact]
    public void ANonceReusedByADifferentCommandIsReplayed()
    {
        var store = new DispatchDedupeStore(StatePath);
        store.Admit("cmd-1", "shared-nonce", 1).ShouldBe(DispatchAdmission.Execute);

        store.Admit("cmd-2", "shared-nonce", 2).ShouldBe(DispatchAdmission.Replayed);
        store.HighWater.ShouldBe(1);
    }

    [Fact]
    public void TheWindowKeepsOnlyTheNewest1024CommandIdsButTheHighWaterStillBlocksOlderSequences()
    {
        var store = new DispatchDedupeStore(StatePath);
        for (var seq = 1; seq <= DispatchDedupeStore.MaxRecentCommands + 1; seq++)
        {
            store.Admit("cmd-" + seq, "n-" + seq, seq).ShouldBe(DispatchAdmission.Execute);
        }

        var restarted = new DispatchDedupeStore(StatePath);

        // cmd-1 left the window, but its sequence is still below the high-water mark.
        restarted.Admit("cmd-1", "n-1", 1).ShouldBe(DispatchAdmission.Duplicate);
        restarted.Admit("cmd-1025", "n-1025", 1025).ShouldBe(DispatchAdmission.Duplicate);
        restarted.Admit("cmd-1026", "n-1026", 1026).ShouldBe(DispatchAdmission.Execute);
    }

    [Fact]
    public void AnUnreadableWindowIsAnErrorAndIsNeverResetSilently()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(StatePath, "{not json");

        Should.Throw<InvalidDataException>(() => new DispatchDedupeStore(StatePath));
    }

    [Fact]
    public void AFailedWriteExecutesNothing()
    {
        // The directory cannot be created over a file of the same name, so the persist step fails.
        Directory.CreateDirectory(_directory);
        var blocked = Path.Combine(_directory, "blocked");
        File.WriteAllText(blocked, "x");
        var store = new DispatchDedupeStore(Path.Combine(blocked, "dedupe.json"));

        Should.Throw<Exception>(() => store.Admit("cmd-1", "n-1", 1));
        store.HighWater.ShouldBe(0);
    }
}
