using System.Text.Json;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Usage;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Usage;

/// <summary>The local usage ledger: retention, owner-only mode, and no prompt content.</summary>
public sealed class FileUsageLedgerTests
{
    /// <summary>Several sessions append occupancy and cumulative cost, and the stored rows match the reports field by field.</summary>
    [Fact]
    public void AppendStoresOccupancyAndCostWithoutAddingTurns()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var ledger = new FileUsageLedger(Directory.CreateTempSubdirectory("usage-ledger-").FullName, clock);
        ledger.Append("s1", used: 100, size: 1000, new UsageCost(1.00m, "USD"));
        ledger.Append("s1", used: 250, size: 1000, new UsageCost(1.50m, "USD"));
        ledger.Append("s2", used: 40, size: 800, new UsageCost(0.25m, "USD"));
        clock.Now = clock.Now.AddDays(1);
        ledger.Append("s1", used: 80, size: 1000, new UsageCost(2.00m, "USD"));

        var rows = ledger.Read();
        rows.Count.ShouldBe(4);
        rows[1].Used.ShouldBe(250);
        rows[3].Used.ShouldBe(80);
        rows.Where(row => string.Equals(row.SessionId, "s1", StringComparison.Ordinal)).Max(row => row.Used).ShouldBe(250);
        rows.Where(row => string.Equals(row.SessionId, "s1", StringComparison.Ordinal)).Sum(row => row.Used).ShouldNotBe(rows.Where(row => string.Equals(row.SessionId, "s1", StringComparison.Ordinal)).Last().Used);
        rows.Last(row => string.Equals(row.SessionId, "s1", StringComparison.Ordinal)).Cost.ShouldBe(new UsageCost(2.00m, "USD"));
        rows.Single(row => string.Equals(row.SessionId, "s2", StringComparison.Ordinal)).Cost.ShouldBe(new UsageCost(0.25m, "USD"));
        rows.Where(row => row.Day == new DateOnly(2026, 10, 1) && row.Cost is not null).Sum(row => row.Cost!.Amount).ShouldBe(1.00m + 1.50m + 0.25m);
    }

    /// <summary>Rows older than 90 days are dropped on the next append, and a row has no content fields.</summary>
    [Fact]
    public void AppendPrunesRowsOlderThanNinetyDaysAndStoresNoContent()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var ledger = new FileUsageLedger(Directory.CreateTempSubdirectory("usage-prune-").FullName, clock);
        ledger.Append("old", 10, 100, null);
        clock.Now = clock.Now.AddDays(90);
        ledger.Append("kept-edge", 11, 100, null);
        clock.Now = clock.Now.AddDays(1);
        ledger.Append("fresh", 12, 100, new UsageCost(0.5m, "USD"));

        var rows = ledger.Read();
        rows.Select(row => row.SessionId).ShouldBe(["kept-edge", "fresh"]);
        var names = typeof(UsageLedgerEntry).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
        names.ShouldBe(["Cost", "Day", "SessionId", "Size", "Used"]);
        var text = File.ReadAllText(ledger.LedgerPath);
        text.ShouldNotContain("prompt", Case.Insensitive);
        text.ShouldNotContain("arguments", Case.Insensitive);
        text.ShouldNotContain("result", Case.Insensitive);
        JsonDocument.Parse(text.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0]).RootElement.EnumerateObject().Select(property => property.Name).ShouldNotContain("prompt", StringComparer.Ordinal);
    }

    /// <summary>The file is mode 0600, and concurrent appends through the ledger lock stay one object per line.</summary>
    [Fact]
    public void TheFileIsOwnerOnlyAndConcurrentAppendsDoNotInterleave()
    {
        var ledger = new FileUsageLedger(Directory.CreateTempSubdirectory("usage-lock-").FullName, new FixedClock(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero)));
        Parallel.For(0, 24, index => ledger.Append("s" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), index, 1000, new UsageCost(index, "USD")));

        var lines = File.ReadAllLines(ledger.LedgerPath);
        lines.Length.ShouldBe(24);
        foreach (var line in lines)
        {
            var parsed = JsonDocument.Parse(line);
            parsed.RootElement.GetProperty("sessionId").GetString().ShouldNotBeNullOrWhiteSpace();
            line.ShouldNotContain("\n");
        }

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(ledger.LedgerPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        ledger.Read().Count.ShouldBe(24);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
