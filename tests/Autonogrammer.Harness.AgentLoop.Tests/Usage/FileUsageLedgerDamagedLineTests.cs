using Autonogrammer.Harness.AgentLoop.Usage;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Usage;

/// <summary>A damaged line in the usage ledger is skipped, so appends and reads keep working, and the next write drops it.</summary>
public sealed class FileUsageLedgerDamagedLineTests
{
    [Fact]
    public void AnUnparsableLineDoesNotStopAppendOrReadAndIsDroppedByTheNextWrite()
    {
        var ledger = new FileUsageLedger(Directory.CreateTempSubdirectory("usage-damaged-").FullName);
        ledger.Append("s1", used: 100, size: 1000, cost: null);
        File.AppendAllText(ledger.LedgerPath, "{ truncated\n");

        ledger.Append("s2", used: 200, size: 1000, cost: null);

        ledger.Read().Select(row => row.SessionId).ShouldBe(["s1", "s2"]);
        File.ReadAllText(ledger.LedgerPath).ShouldNotContain("truncated");
    }
}
