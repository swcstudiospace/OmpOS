using Autonogrammer.Harness.AgentLoop.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Sessions;

/// <summary>
/// A session state file that exists but cannot be read is left in place, is not quarantined, and no later write replaces it, so
/// the sessions it holds are still there for a load that can read it.
/// </summary>
public sealed class AgentSessionUnreadableStateTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnUnreadableFileIsKeptAndNoLaterWriteReplacesIt()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        var first = new AgentSessionRegistry(directory.StatePath, clock);
        first.Create("Kept", null);
        var original = await File.ReadAllBytesAsync(directory.StatePath, Token);

        using (new FileStream(directory.StatePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var registry = await AgentSessionRegistry.OpenAsync(directory.StatePath, clock, null, null, Token);
            registry.SnapshotRecords().ShouldBeEmpty();
            registry.Create("Not persisted", null);
        }

        (await File.ReadAllBytesAsync(directory.StatePath, Token)).ShouldBe(original);
        Directory.GetFiles(directory.Path, "*.quarantined-*").ShouldBeEmpty();
    }
}
