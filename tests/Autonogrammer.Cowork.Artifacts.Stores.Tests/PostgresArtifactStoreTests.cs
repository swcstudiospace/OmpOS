using Autonogrammer.Cowork.Artifacts.Model;
using Autonogrammer.Cowork.Artifacts.Stores.Tests.Support;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Stores.Tests;

/// <summary>Round-trip tests against the M0006 tables. They require the test PostgreSQL server.</summary>
public sealed class PostgresArtifactStoreTests : ArtifactStoreContract, IAsyncLifetime
{
    private readonly PostgresArtifactFixture _fixture = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => _fixture.InitializeAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    /// <inheritdoc />
    protected override IArtifactStore CreateStore() => _fixture.CreateStore("contract-" + Guid.NewGuid().ToString("N"));

    /// <summary>Bytes over 5242880 are a typed quota failure, and the table check rejects them too.</summary>
    [Fact]
    public async Task Content_over_the_cap_is_quota_exceeded_and_the_check_holds()
    {
        var account = "cap-" + Guid.NewGuid().ToString("N");
        using var store = _fixture.CreateStore(account);
        var oversized = new byte[ArtifactWireContract.MaxContentBytes + 1];
        var failure = await Should.ThrowAsync<ArtifactQuotaException>(() => store.MutateAsync(
            mutation =>
            {
                mutation.Insert(
                    new ArtifactRecord("cap-id", Autonogrammer.Contracts.Artifacts.ArtifactKind.Image, "Big", "image/png", null, null, null),
                    new ArtifactVersionRecord("cap-id", 1, oversized, ArtifactWireContract.Hash(oversized), null, null, ArtifactAuthor.Agent, DateTimeOffset.UnixEpoch, null));
                return true;
            },
            Token).AsTask()).ConfigureAwait(true);
        failure.Failure.ShouldBe(ArtifactFailure.QuotaExceeded);

        var admin = Environment.GetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION");
        var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(admin) { SearchPath = _fixture.SchemaName + ",public" }.ConnectionString);
        await using (connection.ConfigureAwait(true))
        {
            await connection.OpenAsync(Token).ConfigureAwait(true);
            var header = new NpgsqlCommand("INSERT INTO artifacts (id, account_id, title, kind, latest_number, created_at, updated_at) VALUES ('check-id', @account, 'Big', 'Image', 1, now(), now())", connection);
            await using (header.ConfigureAwait(true))
            {
                header.Parameters.AddWithValue("account", account);
                _ = await header.ExecuteNonQueryAsync(Token).ConfigureAwait(true);
            }

            var version = new NpgsqlCommand("INSERT INTO artifact_versions (artifact_id, version, session_id, account_id, content_sha256, size_bytes, media_type, author, permissions, created_at, content) VALUES ('check-id', 1, '', @account, 'abc', @size, 'image/png', 'Agent', '[]', now(), @content)", connection);
            await using (version.ConfigureAwait(true))
            {
                version.Parameters.AddWithValue("account", account);
                version.Parameters.AddWithValue("size", oversized.Length);
                version.Parameters.AddWithValue("content", oversized);
                var rejected = await Should.ThrowAsync<PostgresException>(() => version.ExecuteNonQueryAsync(Token)).ConfigureAwait(true);
                rejected.SqlState.ShouldBe("23514");
            }
        }
    }

    /// <summary>Every query path, including grants, hides another account's rows.</summary>
    [Fact]
    public async Task Another_account_cannot_read_rows_or_grants()
    {
        using var left = _fixture.CreateStore("left-" + Guid.NewGuid().ToString("N"));
        using var right = _fixture.CreateStore("right-" + Guid.NewGuid().ToString("N"));
        var id = Guid.NewGuid().ToString("D");
        await left.MutateAsync(
            mutation =>
            {
                mutation.Insert(
                    new ArtifactRecord(id, Autonogrammer.Contracts.Artifacts.ArtifactKind.Markdown, "Secret", "text/markdown", null, "session", null),
                    new ArtifactVersionRecord(id, 1, "hidden"u8.ToArray(), ArtifactWireContract.Hash("hidden"u8), null, null, ArtifactAuthor.Agent, DateTimeOffset.UnixEpoch, null));
                return true;
            },
            Token).ConfigureAwait(true);
        await left.SaveGrantAsync(new ArtifactWireContract.Grant(id, 1, ArtifactWireContract.Hash("hidden"u8), ["https://desk.example"], "read", "desk", DateTimeOffset.UnixEpoch), Token).ConfigureAwait(true);

        (await right.GetArtifactAsync(id, Token).ConfigureAwait(true)).ShouldBeNull();
        (await right.GetVersionsAsync(id, Token).ConfigureAwait(true)).ShouldBeEmpty();
        (await right.GrantsAsync(id, Token).ConfigureAwait(true)).ShouldBeEmpty();
        var seen = await left.GrantsAsync(id, Token).ConfigureAwait(true);
        seen.Count.ShouldBe(1);
        seen[0].Device.ShouldBe("desk");
    }

    /// <summary>Writers on separate store instances of one account serialize on the database lock, so a quota decision made from usage holds until commit.</summary>
    [Fact]
    public async Task Parallel_writers_on_separate_stores_share_one_account_quota()
    {
        var account = "quota-" + Guid.NewGuid().ToString("N");
        const int limit = 2;
        var stores = Enumerable.Range(0, 8).Select(_ => _fixture.CreateStore(account)).ToArray();
        try
        {
            var results = await Task.WhenAll(stores.Select((store, index) => store.MutateAsync(
                mutation =>
                {
                    if (mutation.Usage().ArtifactCount >= limit)
                    {
                        return false;
                    }

                    var id = "quota-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
                    mutation.Insert(Header(id), Version(id, 1, "body", null));
                    return true;
                },
                Token).AsTask())).ConfigureAwait(true);

            results.Count(accepted => accepted).ShouldBe(limit);
            (await stores[0].MutateAsync(mutation => mutation.Usage(), Token).ConfigureAwait(true)).ArtifactCount.ShouldBe(limit);
        }
        finally
        {
            foreach (var store in stores)
            {
                store.Dispose();
            }
        }
    }

    /// <summary>Two requests that write the same version number end with one winner and a decision for the loser, never a key violation.</summary>
    [Fact]
    public async Task Parallel_writes_of_one_version_never_reach_a_key_violation()
    {
        var account = "race-" + Guid.NewGuid().ToString("N");
        var id = "race-" + Guid.NewGuid().ToString("N");
        using (var seed = _fixture.CreateStore(account))
        {
            await seed.MutateAsync(
                mutation =>
                {
                    mutation.Insert(Header(id), Version(id, 1, "one", null));
                    return true;
                },
                Token).ConfigureAwait(true);
        }

        var stores = Enumerable.Range(0, 6).Select(_ => _fixture.CreateStore(account)).ToArray();
        try
        {
            var outcomes = await Task.WhenAll(stores.Select((store, index) => store.MutateAsync(
                mutation =>
                {
                    if (mutation.Versions(id).Any(row => row.Number == 2))
                    {
                        return "exists";
                    }

                    mutation.Append(Version(id, 2, "two-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), mutation.Versions(id)[^1]));
                    return "created";
                },
                Token).AsTask())).ConfigureAwait(true);

            outcomes.Count(outcome => string.Equals(outcome, "created", StringComparison.Ordinal)).ShouldBe(1);
            outcomes.Count(outcome => string.Equals(outcome, "exists", StringComparison.Ordinal)).ShouldBe(5);
            (await stores[0].GetVersionsAsync(id, Token).ConfigureAwait(true)).Count.ShouldBe(2);
        }
        finally
        {
            foreach (var store in stores)
            {
                store.Dispose();
            }
        }
    }

    /// <summary>A version that is not the next number is refused, so the parent chain every read verifies stays intact.</summary>
    [Fact]
    public async Task Append_requires_the_next_version_number()
    {
        using var store = _fixture.CreateStore("order-" + Guid.NewGuid().ToString("N"));
        var id = "order-" + Guid.NewGuid().ToString("N");
        await store.MutateAsync(
            mutation =>
            {
                mutation.Insert(Header(id), Version(id, 1, "one", null));
                return true;
            },
            Token).ConfigureAwait(true);

        await Should.ThrowAsync<ArtifactIntegrityException>(() => store.MutateAsync(
            mutation =>
            {
                mutation.Append(Version(id, 3, "three", mutation.Versions(id)[^1]));
                return true;
            },
            Token).AsTask()).ConfigureAwait(true);

        (await store.GetVersionsAsync(id, Token).ConfigureAwait(true)).Count.ShouldBe(1);
    }

    /// <summary>A write reads usage with aggregates and loads only the artifact it changes, never another artifact's bytes.</summary>
    [Fact]
    public async Task A_write_loads_only_the_artifact_it_changes()
    {
        var account = "scope-" + Guid.NewGuid().ToString("N");
        using var logger = new CommandTextLoggerFactory();
        using var store = _fixture.CreateStore(account, logger);
        var other = "other-" + Guid.NewGuid().ToString("N");
        var target = "target-" + Guid.NewGuid().ToString("N");
        var big = new string('x', 100_000);
        await store.MutateAsync(
            mutation =>
            {
                mutation.Insert(Header(other), Version(other, 1, big, null));
                mutation.Insert(Header(target), Version(target, 1, "one", null));
                return true;
            },
            Token).ConfigureAwait(true);
        logger.Clear();

        var usage = await store.MutateAsync(
            mutation =>
            {
                var before = mutation.Usage();
                mutation.Append(Version(target, 2, "two", mutation.Versions(target)[^1]));
                return (before, after: mutation.Usage());
            },
            Token).ConfigureAwait(true);

        usage.before.ShouldBe(new ArtifactUsage(2, big.Length + 3));
        usage.after.ShouldBe(new ArtifactUsage(2, big.Length + 3 + 3));
        var statements = logger.Messages.Where(message => message.Contains("artifact_versions", StringComparison.Ordinal)).ToArray();
        statements.ShouldNotBeEmpty();
        statements.ShouldAllBe(message => message.Contains("sum(size_bytes)", StringComparison.Ordinal) || message.Contains("a.id = $1", StringComparison.Ordinal) || message.Contains("WHERE artifact_id = $1", StringComparison.Ordinal) || message.Contains("INSERT INTO artifact_versions", StringComparison.Ordinal));
        (await store.GetVersionsAsync(target, Token).ConfigureAwait(true)).Count.ShouldBe(2);
    }

    private static ArtifactRecord Header(string id) => new(id, Autonogrammer.Contracts.Artifacts.ArtifactKind.Markdown, "Title", "text/markdown", null, "session", null);

    private static ArtifactVersionRecord Version(string id, int number, string text, ArtifactVersionRecord? parent)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return new ArtifactVersionRecord(id, number, bytes, ArtifactWireContract.Hash(bytes), parent?.Number, parent?.ContentSha256, ArtifactAuthor.Agent, DateTimeOffset.UnixEpoch, null);
    }
}
