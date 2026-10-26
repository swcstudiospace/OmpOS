using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Sessions.Tests;

/// <summary>
/// Behavioral contract every <see cref="ISessionRepository"/> must satisfy. Derive a concrete class that supplies a fresh,
/// empty repository and, optionally, its teardown. Public so other repositories can reuse the same contract.
/// </summary>
/// <remarks>
/// Every test creates its repositories through <see cref="CreateRepositoryAsync"/>. Each repository a test created is passed
/// to <see cref="DisposeRepositoryAsync"/> once the test has finished.
/// </remarks>
/// <typeparam name="T">The repository type under test.</typeparam>
public abstract class SessionRepositoryContractTests<T> : IAsyncLifetime where T : ISessionRepository
{
    private static readonly DateTimeOffset Epoch = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    private readonly List<T> _repositories = [];

    /// <summary>Creates a fresh, empty repository.</summary>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>An empty repository.</returns>
    protected abstract Task<T> CreateRepositoryAsync(CancellationToken cancellationToken);

    /// <summary>Tears down a repository created by <see cref="CreateRepositoryAsync"/>. Called once per repository, after its test.</summary>
    /// <param name="repository">The repository to tear down.</param>
    /// <returns>A task that completes when the teardown has finished.</returns>
    protected virtual ValueTask DisposeRepositoryAsync(T repository) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var repository in _repositories)
        {
            await DisposeRepositoryAsync(repository).ConfigureAwait(false);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>Getting an identifier that was never saved returns null.</summary>
    [Fact]
    public async Task GetMissingSessionReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);

        var session = await repository.GetAsync("absent", ct).ConfigureAwait(true);

        session.ShouldBeNull();
    }

    /// <summary>A saved session comes back with every field equal, including messages and events.</summary>
    [Fact]
    public async Task SaveThenGetRoundTripsFullSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);
        var saved = Sample("s-1") with
        {
            Messages =
            [
                new Message { Role = "user", Content = "hi", Timestamp = Epoch },
                new Message { Role = "assistant", Content = "hello", Timestamp = Epoch.AddSeconds(1) },
            ],
            Events =
            [
                Event("s-1", new TextDeltaPayload { Delta = "hello" }) with { Seq = 1 },
            ],
        };

        await repository.SaveAsync(saved, ct).ConfigureAwait(true);
        var loaded = await repository.GetAsync("s-1", ct).ConfigureAwait(true);

        loaded.ShouldNotBeNull();
        ShouldBeSameSession(loaded, saved);
    }

    /// <summary>Appends on a fresh session are numbered 1, 2, 3.</summary>
    [Fact]
    public async Task AppendAssignsSeqFromOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);
        await repository.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);

        for (var i = 0; i < 3; i++)
        {
            await repository.AppendAsync("s-1", Event("s-1", new TextDeltaPayload { Delta = $"{i}" }), ct).ConfigureAwait(true);
        }

        var all = await ListAsync(repository, "s-1", 0, ct).ConfigureAwait(true);
        all.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L, 3L]);
    }

    /// <summary>Ten parallel appends produce exactly Seq 1..10: no gaps and no duplicates.</summary>
    [Fact]
    public async Task ConcurrentAppendsGetExactlyOneToTen()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);
        await repository.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            Task.Run(() => repository.AppendAsync("s-1", Event("s-1", new TextDeltaPayload { Delta = $"{i}" }), ct), ct))).ConfigureAwait(true);

        var all = await ListAsync(repository, "s-1", 0, ct).ConfigureAwait(true);
        all.Select(static evt => evt.Seq).Order().ToArray().ShouldBe(Enumerable.Range(1, 10).Select(static i => (long)i).ToArray());
    }

    /// <summary>ListFromSeq(2) returns the events numbered 2 onward, inclusive of 2.</summary>
    [Fact]
    public async Task ListFromSeqIsInclusiveOfStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 4, ct).ConfigureAwait(true);

        var tail = await ListAsync(repository, "s-1", 2, ct).ConfigureAwait(true);

        tail.Select(static evt => evt.Seq).ToArray().ShouldBe([2L, 3L, 4L]);
    }

    /// <summary>ListFromSeq(0) returns the whole log.</summary>
    [Fact]
    public async Task ListFromSeqZeroReturnsAll()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 4, ct).ConfigureAwait(true);

        var all = await ListAsync(repository, "s-1", 0, ct).ConfigureAwait(true);

        all.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L, 3L, 4L]);
    }

    /// <summary>ListFromSeq beyond the last Seq yields nothing.</summary>
    [Fact]
    public async Task ListFromSeqPastEndYieldsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 3, ct).ConfigureAwait(true);

        var none = await ListAsync(repository, "s-1", 4, ct).ConfigureAwait(true);

        none.ShouldBeEmpty();
    }

    /// <summary>Sequence numbers are per session: appending to one session does not advance another.</summary>
    [Fact]
    public async Task SeqIsIndependentPerSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 2, ct).ConfigureAwait(true);
        await repository.SaveAsync(Sample("s-2"), ct).ConfigureAwait(true);

        await repository.AppendAsync("s-2", Event("s-2", new TextDeltaPayload { Delta = "x" }), ct).ConfigureAwait(true);

        var other = await ListAsync(repository, "s-2", 0, ct).ConfigureAwait(true);
        other.Select(static evt => evt.Seq).ToArray().ShouldBe([1L]);
    }

    /// <summary>Appending to a session that was never saved throws <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    public async Task AppendToUnknownSessionThrowsKeyNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);

        await Should.ThrowAsync<KeyNotFoundException>(() =>
            repository.AppendAsync("absent", Event("absent", new TextDeltaPayload { Delta = "x" }), ct)).ConfigureAwait(true);
    }

    /// <summary>Saving a session whose event list is shorter than the stored log keeps the stored log.</summary>
    [Fact]
    public async Task SaveNeverShrinksTheLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 3, ct).ConfigureAwait(true);
        var stale = Sample("s-1") with { Status = SessionStatus.Running };

        await repository.SaveAsync(stale, ct).ConfigureAwait(true);

        var loaded = await repository.GetAsync("s-1", ct).ConfigureAwait(true);
        loaded.ShouldNotBeNull();
        loaded.Status.ShouldBe(SessionStatus.Running);
        loaded.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L, 3L]);
    }

    /// <summary>Saving an equal-length log whose Seq diverges from the stored log is rejected and keeps the stored log.</summary>
    [Fact]
    public async Task SaveWithEqualLengthConflictingLogThrows()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 3, ct).ConfigureAwait(true);
        var clashing = Sample("s-1") with
        {
            Status = SessionStatus.Running,
            Events =
            [
                Event("s-1", new TextDeltaPayload { Delta = "stale-0" }) with { Seq = 11 },
                Event("s-1", new TextDeltaPayload { Delta = "stale-1" }) with { Seq = 12 },
                Event("s-1", new TextDeltaPayload { Delta = "stale-2" }) with { Seq = 13 },
            ],
        };

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            repository.SaveAsync(clashing, ct)).ConfigureAwait(true);

        error.Message.ShouldContain("s-1");
        var loaded = await repository.GetAsync("s-1", ct).ConfigureAwait(true);
        loaded.ShouldNotBeNull();
        loaded.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L, 3L]);
    }

    /// <summary>Saving a longer log whose Seq prefix matches the stored log replaces it.</summary>
    [Fact]
    public async Task SaveWithLongerExtendingLogReplaces()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 3, ct).ConfigureAwait(true);
        var loaded = await repository.GetAsync("s-1", ct).ConfigureAwait(true);
        loaded.ShouldNotBeNull();
        var extended = loaded with
        {
            Status = SessionStatus.Running,
            Events = [.. loaded.Events, Event("s-1", new TextDeltaPayload { Delta = "extra" }) with { Seq = 4 }],
        };

        await repository.SaveAsync(extended, ct).ConfigureAwait(true);

        var stored = await repository.GetAsync("s-1", ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(SessionStatus.Running);
        stored.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L, 3L, 4L]);
    }

    /// <summary>A session lock admits one holder at a time: concurrent holders never overlap.</summary>
    [Fact]
    public async Task SessionLockAdmitsOneHolderAtATime()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);
        var inside = 0;
        var overlaps = 0;

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            var lease = await repository.LockSessionAsync("s-1", ct).ConfigureAwait(true);
            try
            {
                if (Interlocked.Increment(ref inside) > 1)
                {
                    Interlocked.Increment(ref overlaps);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(5), ct).ConfigureAwait(false);
                Interlocked.Decrement(ref inside);
            }
            finally
            {
                await lease.DisposeAsync().ConfigureAwait(true);
            }
        }, ct))).ConfigureAwait(true);

        overlaps.ShouldBe(0);
    }

    /// <summary>A second lock on a session waits until the first is released, then is granted.</summary>
    [Fact]
    public async Task SecondSessionLockWaitsUntilFirstIsReleased()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);
        var first = await repository.LockSessionAsync("s-1", ct).ConfigureAwait(true);
        var second = repository.LockSessionAsync("s-1", ct);

        await Task.Delay(TimeSpan.FromMilliseconds(50), ct).ConfigureAwait(true);
        second.IsCompleted.ShouldBeFalse();

        await first.DisposeAsync().ConfigureAwait(true);
        var acquired = await second.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);
        await acquired.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Disposing a session lock twice releases it once: a later holder is admitted and a further waiter still waits.</summary>
    [Fact]
    public async Task DisposingSessionLockTwiceReleasesItOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);
        var lease = await repository.LockSessionAsync("s-1", ct).ConfigureAwait(true);
        await lease.DisposeAsync().ConfigureAwait(true);
        await lease.DisposeAsync().ConfigureAwait(true);

        var held = await repository.LockSessionAsync("s-1", ct).ConfigureAwait(true);
        var blocked = repository.LockSessionAsync("s-1", ct);
        await Task.Delay(TimeSpan.FromMilliseconds(50), ct).ConfigureAwait(true);
        blocked.IsCompleted.ShouldBeFalse();

        await held.DisposeAsync().ConfigureAwait(true);
        var admitted = await blocked.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);
        await admitted.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>A session lock can be taken for an identifier that has no saved session yet.</summary>
    [Fact]
    public async Task SessionLockIsAvailableForSessionThatDoesNotExistYet()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);

        var lease = await repository.LockSessionAsync("absent", ct).ConfigureAwait(true);
        await lease.DisposeAsync().ConfigureAwait(true);

        var session = await repository.GetAsync("absent", ct).ConfigureAwait(true);
        session.ShouldBeNull();
    }

    /// <summary>A pending approval is stored and read back with every field, and has no decision.</summary>
    [Fact]
    public async Task StoredApprovalRoundTripsPendingRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 0, ct).ConfigureAwait(true);
        var record = Pending("s-1", "req-1");

        (await repository.TryAddToolApprovalAsync("s-1", record, ct).ConfigureAwait(true)).ShouldBeTrue();

        var loaded = await repository.GetToolApprovalAsync("s-1", "req-1", ct).ConfigureAwait(true);
        loaded.ShouldNotBeNull();
        loaded.SessionId.ShouldBe("s-1");
        loaded.RequestId.ShouldBe("req-1");
        loaded.ToolName.ShouldBe("Bash");
        loaded.GrantableTools.ToArray().ShouldBe(["Bash"]);
        loaded.RequestedAt.ShouldBe(Epoch);
        loaded.Decision.ShouldBeNull();
        loaded.DecidedAt.ShouldBeNull();
    }

    /// <summary>A request identifier is added once per session: a duplicate is refused and other identifiers are unaffected.</summary>
    [Fact]
    public async Task TryAddToolApprovalRefusesDuplicateRequestId()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 0, ct).ConfigureAwait(true);

        (await repository.TryAddToolApprovalAsync("s-1", Pending("s-1", "req-1"), ct).ConfigureAwait(true)).ShouldBeTrue();
        (await repository.TryAddToolApprovalAsync("s-1", Pending("s-1", "req-1"), ct).ConfigureAwait(true)).ShouldBeFalse();
        (await repository.TryAddToolApprovalAsync("s-1", Pending("s-1", "req-2"), ct).ConfigureAwait(true)).ShouldBeTrue();
    }

    /// <summary>Adding an approval to a session that was never saved throws <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    public async Task AddingApprovalToUnknownSessionThrowsKeyNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);

        await Should.ThrowAsync<KeyNotFoundException>(() =>
            repository.TryAddToolApprovalAsync("absent", Pending("absent", "req-1"), ct)).ConfigureAwait(true);
    }

    /// <summary>A pending approval is decided once: the first decision is recorded, and a later one is refused.</summary>
    [Fact]
    public async Task TryDecideToolApprovalDecidesOnlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 0, ct).ConfigureAwait(true);
        await repository.TryAddToolApprovalAsync("s-1", Pending("s-1", "req-1"), ct).ConfigureAwait(true);
        var decidedAt = Epoch.AddMinutes(1);

        (await repository.TryDecideToolApprovalAsync("s-1", "req-1", PermissionDecision.Allow, decidedAt, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await repository.TryDecideToolApprovalAsync("s-1", "req-1", PermissionDecision.Deny, decidedAt.AddSeconds(1), ct).ConfigureAwait(true)).ShouldBeFalse();

        var decided = await repository.GetToolApprovalAsync("s-1", "req-1", ct).ConfigureAwait(true);
        decided.ShouldNotBeNull();
        decided.Decision.ShouldBe(PermissionDecision.Allow);
        decided.DecidedAt.ShouldBe(decidedAt);
    }

    /// <summary>Deciding an unknown request is refused; getting an unknown request or session returns null.</summary>
    [Fact]
    public async Task UnknownApprovalCannotBeDecidedAndIsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await SeededAsync("s-1", 0, ct).ConfigureAwait(true);

        (await repository.TryDecideToolApprovalAsync("s-1", "absent", PermissionDecision.Deny, Epoch, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await repository.GetToolApprovalAsync("s-1", "absent", ct).ConfigureAwait(true)).ShouldBeNull();
        (await repository.GetToolApprovalAsync("no-session", "req-1", ct).ConfigureAwait(true)).ShouldBeNull();
        (await repository.TryDecideToolApprovalAsync("no-session", "req-1", PermissionDecision.Deny, Epoch, ct).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>Lists only sessions with the requested status that were last updated strictly before the moment, in identifier order.</summary>
    [Fact]
    public async Task ListSessionIdsByStatusReturnsOnlyMatchingSessionsUpdatedBeforeTheMoment()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);
        var threshold = Epoch.AddMinutes(10);
        await repository.SaveAsync(Sample("b-idle-old"), ct).ConfigureAwait(true);
        await repository.SaveAsync(Sample("a-idle-old"), ct).ConfigureAwait(true);
        await repository.SaveAsync(Sample("c-idle-recent") with { UpdatedAt = Epoch.AddMinutes(20) }, ct).ConfigureAwait(true);
        await repository.SaveAsync(Sample("d-running-old") with { Status = SessionStatus.Running }, ct).ConfigureAwait(true);
        await repository.SaveAsync(Sample("e-idle-at-threshold") with { UpdatedAt = threshold }, ct).ConfigureAwait(true);

        var ids = await repository.ListSessionIdsByStatusAsync(SessionStatus.Idle, threshold, ct).ConfigureAwait(true);

        ids.ToArray().ShouldBe(["a-idle-old", "b-idle-old"]);
    }

    /// <summary>At most 200 matching sessions are returned, and they are the first 200 by identifier, not by save order.</summary>
    [Fact]
    public async Task ListSessionIdsByStatusIsBoundedAndOrderedById()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(true);
        for (var index = 204; index >= 0; index--)
        {
            // Saved in descending order, so a result ordered by insertion would not match.
            await repository.SaveAsync(Sample($"s-{index:D3}"), ct).ConfigureAwait(true);
        }

        var ids = await repository.ListSessionIdsByStatusAsync(SessionStatus.Idle, Epoch.AddSeconds(1), ct).ConfigureAwait(true);

        ids.ToArray().ShouldBe(Enumerable.Range(0, 200).Select(static index => $"s-{index:D3}").ToArray());
    }

    /// <summary>Asserts every field of two sessions is equal, comparing collections element by element.</summary>
    /// <param name="actual">The loaded session.</param>
    /// <param name="expected">The session that was saved.</param>
    protected static void ShouldBeSameSession(Session actual, Session expected)
    {
        actual.Id.ShouldBe(expected.Id);
        actual.OwnerId.ShouldBe(expected.OwnerId);
        actual.CreatedAt.ShouldBe(expected.CreatedAt);
        actual.UpdatedAt.ShouldBe(expected.UpdatedAt);
        actual.Status.ShouldBe(expected.Status);
        actual.SandboxId.ShouldBe(expected.SandboxId);
        actual.Messages.ShouldBe(expected.Messages);
        actual.Events.ShouldBe(expected.Events);
    }

    private static Session Sample(string id) => new()
    {
        Id = id,
        OwnerId = "dev",
        CreatedAt = Epoch,
        UpdatedAt = Epoch,
        Status = SessionStatus.Idle,
        SandboxId = null,
        Messages = [],
        Events = [],
    };

    private static ToolApprovalRecord Pending(string sessionId, string requestId) => new()
    {
        SessionId = sessionId,
        RequestId = requestId,
        ToolName = "Bash",
        GrantableTools = ["Bash"],
        RequestedAt = Epoch,
    };

    private static StreamEvent Event(string sessionId, StreamEventPayload payload) => new()
    {
        Seq = 0,
        SessionId = sessionId,
        CorrelationId = $"{sessionId}/turn-1",
        Timestamp = Epoch,
        Payload = payload,
    };

    /// <summary>Creates a fresh repository and records it for teardown after the test.</summary>
    private async Task<T> NewRepositoryAsync(CancellationToken cancellationToken)
    {
        var repository = await CreateRepositoryAsync(cancellationToken).ConfigureAwait(false);
        _repositories.Add(repository);
        return repository;
    }

    private async Task<T> SeededAsync(string id, int count, CancellationToken ct)
    {
        var repository = await NewRepositoryAsync(ct).ConfigureAwait(false);
        await repository.SaveAsync(Sample(id), ct).ConfigureAwait(false);
        for (var i = 0; i < count; i++)
        {
            await repository.AppendAsync(id, Event(id, new TextDeltaPayload { Delta = $"{i}" }), ct).ConfigureAwait(false);
        }

        return repository;
    }

    private static async Task<List<StreamEvent>> ListAsync(ISessionRepository repository, string id, long fromSeq, CancellationToken ct)
    {
        var events = new List<StreamEvent>();
        await foreach (var evt in repository.ListFromSeqAsync(id, fromSeq, ct).ConfigureAwait(true))
        {
            events.Add(evt);
        }

        return events;
    }
}
