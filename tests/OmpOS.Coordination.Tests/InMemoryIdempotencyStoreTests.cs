using Shouldly;
using Xunit;

namespace OmpOS.Coordination.Tests;

/// <summary>Claim, replay, conflict, release and expiry behaviour of the process-local idempotency store.</summary>
public sealed class InMemoryIdempotencyStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A second claim with the same fingerprint while the first is pending reports in progress.</summary>
    [Fact]
    public async Task SameFingerprintWhilePendingIsInProgress()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryIdempotencyStore(new ManualTimeProvider(Start));

        var first = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);
        var second = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);

        first.Outcome.ShouldBe(IdempotencyOutcome.Claimed);
        second.Outcome.ShouldBe(IdempotencyOutcome.InProgress);
        second.ResponseJson.ShouldBeNull();
    }

    /// <summary>A different fingerprint against a pending claim is a conflict.</summary>
    [Fact]
    public async Task DifferentFingerprintWhilePendingIsAConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryIdempotencyStore(new ManualTimeProvider(Start));
        _ = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);

        var other = await store.ClaimAsync("start", "k-1", "fp-2", ct).ConfigureAwait(true);

        other.Outcome.ShouldBe(IdempotencyOutcome.Conflict);
    }

    /// <summary>A completed record replays its response for the same fingerprint and conflicts for another.</summary>
    [Fact]
    public async Task CompletedRecordReplaysItsResponseForTheSameFingerprint()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryIdempotencyStore(new ManualTimeProvider(Start));
        _ = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);
        await store.CompleteAsync("start", "k-1", "fp-1", """{"ok":true}""", ct).ConfigureAwait(true);

        var replay = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);
        var conflict = await store.ClaimAsync("start", "k-1", "fp-2", ct).ConfigureAwait(true);

        replay.Outcome.ShouldBe(IdempotencyOutcome.Replay);
        replay.ResponseJson.ShouldBe("""{"ok":true}""");
        conflict.Outcome.ShouldBe(IdempotencyOutcome.Conflict);
    }

    /// <summary>Releasing a pending claim lets the key be claimed again.</summary>
    [Fact]
    public async Task ReleaseDropsAPendingClaimSoTheKeyCanBeReclaimed()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryIdempotencyStore(new ManualTimeProvider(Start));
        _ = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);

        await store.ReleaseAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);

        (await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Claimed);
    }

    /// <summary>A release with another fingerprint leaves the pending claim in place.</summary>
    [Fact]
    public async Task ReleaseWithAnotherFingerprintLeavesThePendingClaim()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryIdempotencyStore(new ManualTimeProvider(Start));
        _ = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);

        await store.ReleaseAsync("start", "k-1", "fp-2", ct).ConfigureAwait(true);

        (await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.InProgress);
    }

    /// <summary>Releasing never removes a completed record.</summary>
    [Fact]
    public async Task ReleaseNeverRemovesACompletedRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryIdempotencyStore(new ManualTimeProvider(Start));
        _ = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);
        await store.CompleteAsync("start", "k-1", "fp-1", "{}", ct).ConfigureAwait(true);

        await store.ReleaseAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);

        (await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Replay);
    }

    /// <summary>A pending claim expires after sixty seconds, and then the key can be claimed again.</summary>
    [Fact]
    public async Task PendingClaimExpiresAfterSixtySeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(Start);
        var store = new InMemoryIdempotencyStore(clock);
        _ = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);

        clock.Advance(TimeSpan.FromSeconds(59));
        (await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.InProgress);

        clock.Advance(TimeSpan.FromSeconds(1));
        (await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Claimed);
    }

    /// <summary>A completed response is replayable for twenty-four hours.</summary>
    [Fact]
    public async Task CompletedRecordExpiresAfterTwentyFourHours()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(Start);
        var store = new InMemoryIdempotencyStore(clock);
        _ = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);
        await store.CompleteAsync("start", "k-1", "fp-1", "{}", ct).ConfigureAwait(true);

        clock.Advance(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59));
        (await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Replay);

        clock.Advance(TimeSpan.FromMinutes(1));
        (await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Claimed);
    }

    /// <summary>The pipe separates fingerprint from response in the Dragonfly layout, so it is refused everywhere.</summary>
    [Fact]
    public async Task FingerprintContainingThePipeIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryIdempotencyStore(new ManualTimeProvider(Start));

        await Should.ThrowAsync<ArgumentException>(() => store.ClaimAsync("start", "k-1", "fp|1", ct)).ConfigureAwait(true);
    }

    /// <summary>A completion that arrives after its pending claim expired must not overwrite a newer claim.</summary>
    [Fact]
    public async Task CompleteAfterPendingClaimExpiredDoesNotOverwriteNewerClaim()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(Start);
        var store = new InMemoryIdempotencyStore(clock);
        _ = await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true);

        clock.Advance(CoordinationDefaults.IdempotencyPendingTimeToLive);
        (await store.ClaimAsync("start", "k-1", "fp-2", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Claimed);

        await store.CompleteAsync("start", "k-1", "fp-1", """{"stale":true}""", ct).ConfigureAwait(true);

        (await store.ClaimAsync("start", "k-1", "fp-2", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.InProgress);
        (await store.ClaimAsync("start", "k-1", "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Conflict);
    }
}
