using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Cowork.Sync;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests;

/// <summary>Sync debounces, keeps the latest offline summary, and marks conflicts quietly.</summary>
public sealed class CoworkSessionSyncTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ABurstBecomesOnePutOfTheLatestSummary()
    {
        var clock = new ManualClock(Start);
        var api = new ScriptedApi();
        using var sync = new CoworkSessionSync(api, clock);
        sync.Publish(Summary("s-1", "one"));
        sync.Publish(Summary("s-1", "two"));
        sync.Publish(Summary("s-1", "three"));

        clock.Advance(TimeSpan.FromSeconds(4));
        await sync.IdleAsync();
        api.Puts.ShouldBeEmpty();

        clock.Advance(TimeSpan.FromSeconds(1));
        await sync.IdleAsync();
        api.Puts.Count.ShouldBe(1);
        api.Puts[0].Title.ShouldBe("three");
        sync.Mark("s-1").ShouldBeNull();
    }

    [Fact]
    public async Task OfflineKeepsTheLatestSummaryAndRetriesWithCappedBackoff()
    {
        var clock = new ManualClock(Start);
        var api = new ScriptedApi { Offline = true };
        using var sync = new CoworkSessionSync(api, clock);
        sync.Publish(Summary("s-1", "first"));
        clock.Advance(CoworkSessionSync.Debounce);
        await sync.IdleAsync();
        api.Puts.Count.ShouldBe(1);

        sync.Publish(Summary("s-1", "latest"));
        clock.Advance(TimeSpan.FromSeconds(1));
        await sync.IdleAsync();
        api.Puts.Count.ShouldBe(2);
        api.Puts[1].Title.ShouldBe("latest");
        sync.Mark("s-1").ShouldNotBeNull();
        sync.Mark("s-1")!.Label.ShouldBe(CoworkSessionSync.NotSyncedLabel);
        sync.Mark("s-1")!.Reason.ShouldBe("offline");

        var delay = TimeSpan.FromSeconds(2);
        while (delay < CoworkSessionSync.MaxBackoff)
        {
            var before = api.Puts.Count;
            clock.Advance(delay - TimeSpan.FromMilliseconds(1));
            await sync.IdleAsync();
            api.Puts.Count.ShouldBe(before);
            clock.Advance(TimeSpan.FromMilliseconds(1));
            await sync.IdleAsync();
            api.Puts.Count.ShouldBe(before + 1);
            delay = TimeSpan.FromSeconds(Math.Min(CoworkSessionSync.MaxBackoff.TotalSeconds, delay.TotalSeconds * 2));
        }

        var capped = api.Puts.Count;
        clock.Advance(CoworkSessionSync.MaxBackoff - TimeSpan.FromMilliseconds(1));
        await sync.IdleAsync();
        api.Puts.Count.ShouldBe(capped);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await sync.IdleAsync();
        api.Puts.Count.ShouldBe(capped + 1);
        api.Puts[^1].Title.ShouldBe("latest");
    }

    [Fact]
    public async Task AConflictSetsNotSyncedAndLeavesLocalWorkAlone()
    {
        var clock = new ManualClock(Start);
        var api = new ScriptedApi { Conflict = true, Reason = "stale" };
        using var sync = new CoworkSessionSync(api, clock);
        var local = Summary("s-1", "kept locally");
        sync.Publish(local);

        clock.Advance(CoworkSessionSync.Debounce);
        await sync.IdleAsync();
        clock.Advance(CoworkSessionSync.MaxBackoff);
        await sync.IdleAsync();

        api.Puts.Count.ShouldBe(1);
        local.Title.ShouldBe("kept locally");
        var mark = sync.Mark("s-1");
        mark.ShouldNotBeNull();
        mark.Label.ShouldBe("Not synced");
        mark.Reason.ShouldBe("stale");
    }

    private static CoworkSessionSummary Summary(string id, string title) => new()
    {
        Id = id,
        Title = title,
        Status = CoworkSessionStatus.Running,
        CreatedAt = Start,
        UpdatedAt = Start,
    };

    private sealed class ScriptedApi : ICoworkSessionApi
    {
        public List<CoworkSessionSummary> Puts { get; } = [];

        public List<TimeSpan> Gaps { get; } = [];

        public bool Offline { get; set; }

        public bool Conflict { get; set; }

        public string Reason { get; set; } = "offline";

        private DateTimeOffset? _last;

        public Task<CoworkSessionApiResult> PutSummaryAsync(CoworkSessionSummary summary, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            if (_last is { } last)
            {
                Gaps.Add(now - last);
            }

            _last = now;
            Puts.Add(summary);
            if (Offline)
            {
                return Task.FromResult(CoworkSessionApiResult.Unreachable("offline"));
            }

            if (Conflict)
            {
                return Task.FromResult(CoworkSessionApiResult.Conflict(Reason));
            }

            return Task.FromResult(CoworkSessionApiResult.Accepted);
        }

        public Task<CoworkSessionApiResult> DeleteAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(CoworkSessionApiResult.Accepted);
    }
}

