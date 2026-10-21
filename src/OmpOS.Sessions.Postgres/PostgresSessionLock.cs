using Npgsql;

namespace OmpOS.Sessions.Postgres;

/// <summary>
/// An exclusive, cross-process session lock backed by a PostgreSQL session-level advisory lock. The lock lives on
/// the dedicated connection it was taken on; disposing releases it and returns the connection to the pool.
/// </summary>
internal sealed class PostgresSessionLock : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly long _key;
    private int _released;

    /// <summary>Wraps a connection that already holds the advisory lock for <paramref name="key"/>.</summary>
    /// <param name="connection">The dedicated connection holding the lock. Owned by this instance from now on.</param>
    /// <param name="key">The advisory-lock key that the connection holds.</param>
    internal PostgresSessionLock(NpgsqlConnection connection, long key)
    {
        _connection = connection;
        _key = key;
    }

    /// <summary>Releases the advisory lock, then disposes the connection. A second call does nothing.</summary>
    /// <returns>A task that completes when the lock is released.</returns>
    /// <exception cref="InvalidOperationException">The connection did not hold the lock when it was released.</exception>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        try
        {
            var released = await UnlockAsync(_connection, _key, CancellationToken.None).ConfigureAwait(false);
            if (!released)
            {
                throw new InvalidOperationException("The session advisory lock was not held when it was released.");
            }
        }
        finally
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Releases an advisory lock the connection may hold.</summary>
    /// <param name="connection">The connection that took the lock.</param>
    /// <param name="key">The advisory-lock key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when the connection held the lock and released it.</returns>
    internal static async Task<bool> UnlockAsync(NpgsqlConnection connection, long key, CancellationToken cancellationToken)
    {
        var command = PostgresSql.Create(connection, null);
        command.CommandText = "SELECT pg_advisory_unlock(@key)";
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("key", key);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is true;
        }
    }
}
