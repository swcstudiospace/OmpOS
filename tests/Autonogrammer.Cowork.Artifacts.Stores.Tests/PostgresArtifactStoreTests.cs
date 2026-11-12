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
    protected override async ValueTask<IArtifactStore> CreateStoreAsync() =>
        await _fixture.CreateStoreAsync("contract-" + Guid.NewGuid().ToString("N")).ConfigureAwait(true);

    /// <summary>Bytes over 5242880 are a typed quota failure, and the table check rejects them too.</summary>
    [Fact]
    public async Task Content_over_the_cap_is_quota_exceeded_and_the_check_holds()
    {
        var account = "cap-" + Guid.NewGuid().ToString("N");
        using var store = await _fixture.CreateStoreAsync(account).ConfigureAwait(true);
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
        using var left = await _fixture.CreateStoreAsync("left-" + Guid.NewGuid().ToString("N")).ConfigureAwait(true);
        using var right = await _fixture.CreateStoreAsync("right-" + Guid.NewGuid().ToString("N")).ConfigureAwait(true);
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
}
