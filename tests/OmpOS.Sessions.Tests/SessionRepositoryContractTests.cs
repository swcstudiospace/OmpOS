using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using Shouldly;
using Xunit;

namespace OmpOS.Sessions.Tests;

/// <summary>
/// Behavioral contract every <see cref="ISessionRepository"/> must satisfy. Derive a concrete class that
/// supplies a fresh, empty repository. Public so other repositories can reuse the same contract.
/// </summary>
/// <typeparam name="T">The repository type under test.</typeparam>
public abstract class SessionRepositoryContractTests<T> where T : ISessionRepository
{
    private static readonly DateTimeOffset Epoch = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a fresh, empty repository.</summary>
    /// <returns>An empty repository.</returns>
    protected abstract T CreateRepository();

    /// <summary>Getting an identifier that was never saved returns null.</summary>
    [Fact]
    public async Task GetMissingSessionReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = CreateRepository();

        var session = await repository.GetAsync("absent", ct).ConfigureAwait(true);

        session.ShouldBeNull();
    }

    /// <summary>A saved session comes back with every field equal, including messages and events.</summary>
    [Fact]
    public async Task SaveThenGetRoundTripsFullSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = CreateRepository();
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
        var repository = CreateRepository();
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
        var repository = CreateRepository();
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
        var repository = CreateRepository();

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

    private static StreamEvent Event(string sessionId, StreamEventPayload payload) => new()
    {
        Seq = 0,
        SessionId = sessionId,
        CorrelationId = $"{sessionId}/turn-1",
        Timestamp = Epoch,
        Payload = payload,
    };

    private async Task<T> SeededAsync(string id, int count, CancellationToken ct)
    {
        var repository = CreateRepository();
        await repository.SaveAsync(Sample(id), ct).ConfigureAwait(true);
        for (var i = 0; i < count; i++)
        {
            await repository.AppendAsync(id, Event(id, new TextDeltaPayload { Delta = $"{i}" }), ct).ConfigureAwait(true);
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
