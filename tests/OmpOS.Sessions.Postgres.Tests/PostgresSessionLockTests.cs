using Npgsql;
using Shouldly;
using Xunit;
using static OmpOS.Sessions.Postgres.Tests.SessionSamples;

namespace OmpOS.Sessions.Postgres.Tests;

/// <summary>A session lock whose unlock fails must not leave its connection in the pool still holding the lock.</summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresSessionLockTests(PostgresServerFixture server)
{
    private static readonly TimeSpan FreeWithin = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The lock connection is not pooled, so closing it releases a lock it still holds, even with pool reset off. After a failed
    /// unlock the key must be free on the server, and the next acquirer must be admitted.
    /// </summary>
    [Fact]
    public async Task FailedUnlockDiscardsTheConnectionSoTheLockIsFreeForTheNextAcquirer()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            // NoResetOnClose: the pool reset is off for the data source, so no DISCARD ALL runs on pool return either way.
            var connectionString = new NpgsqlConnectionStringBuilder(schema.ConnectionString) { NoResetOnClose = true }.ConnectionString;
            var dataSource = NpgsqlDataSource.Create(connectionString);
            try
            {
                await PostgresSessionSchema.EnsureCreatedAsync(dataSource, ct).ConfigureAwait(true);
                var repository = new PostgresSessionRepository(dataSource, schema.Connection);
                await repository.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);

                var key = PostgresSql.AdvisoryKey("s-1");
                // The lock connection is opened the way production opens it: unpooled, so closing it ends the backend.
                var connection = await PostgresSessionRepository.OpenLockConnectionAsync(schema.Connection, ct).ConfigureAwait(true);
                using (var acquire = connection.CreateCommand())
                {
                    acquire.CommandText = "SELECT pg_advisory_lock(@key)";
                    acquire.Parameters.AddWithValue("key", key);
                    await acquire.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
                }

                // The unlock fails after the lock was taken, so this connection still holds it.
                var failing = new PostgresSessionLock(
                    connection,
                    key,
                    static (_, _, _) => Task.FromException<bool>(new InvalidOperationException("forced unlock failure")));
                try
                {
                    await Should.ThrowAsync<InvalidOperationException>(() => failing.DisposeAsync().AsTask()).ConfigureAwait(true);
                }
                finally
                {
                    await failing.DisposeAsync().ConfigureAwait(true);
                }

                (await IsFreeWithinAsync(schema.ConnectionString, key, FreeWithin, ct).ConfigureAwait(true)).ShouldBeTrue();

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(FreeWithin);
                var next = await repository.LockSessionAsync("s-1", timeout.Token).ConfigureAwait(true);
                await next.DisposeAsync().ConfigureAwait(true);
            }
            finally
            {
                await dataSource.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Reports whether no session holds the advisory lock, polling until the deadline. A discarded backend releases its locks as
    /// it exits, which can trail the client closing the socket.
    /// </summary>
    /// <param name="connectionString">The administrative connection string of the test database.</param>
    /// <param name="key">The advisory-lock key.</param>
    /// <param name="within">How long to keep checking.</param>
    /// <param name="ct">Cancels the check.</param>
    /// <returns><see langword="true"/> when the lock could be taken by this probe, which then releases it.</returns>
    private static async Task<bool> IsFreeWithinAsync(string connectionString, long key, TimeSpan within, CancellationToken ct)
    {
        var probe = new NpgsqlConnection(connectionString);
        try
        {
            await probe.OpenAsync(ct).ConfigureAwait(true);
            var deadline = DateTimeOffset.UtcNow + within;
            while (true)
            {
                using (var take = probe.CreateCommand())
                {
                    take.CommandText = "SELECT pg_try_advisory_lock(@key)";
                    take.Parameters.AddWithValue("key", key);
                    if (await take.ExecuteScalarAsync(ct).ConfigureAwait(true) is true)
                    {
                        using var release = probe.CreateCommand();
                        release.CommandText = "SELECT pg_advisory_unlock(@key)";
                        release.Parameters.AddWithValue("key", key);
                        await release.ExecuteScalarAsync(ct).ConfigureAwait(true);
                        return true;
                    }
                }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    return false;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(true);
            }
        }
        finally
        {
            await probe.DisposeAsync().ConfigureAwait(true);
        }
    }
}
