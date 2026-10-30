using Npgsql;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// An exclusive, cross-process session lock backed by a PostgreSQL session-level advisory lock. The lock lives on the
/// dedicated connection it was taken on. That connection is not pooled. Disposing releases the lock and closes the
/// connection.
/// </summary>
/// <remarks>
/// <para>
/// A session-level advisory lock belongs to a server backend. Closing an unpooled connection sends Terminate and closes
/// the socket, so the backend exits, and the server releases every session lock it held. Closing is therefore what
/// guarantees the release, whether or not the unlock ran or succeeded. A pooled connection cannot give that guarantee:
/// returning it to the pool keeps its backend, and so the lock, alive. The pool reset that would clear the lock
/// (<c>DISCARD ALL</c>) does not run when <see cref="NpgsqlConnectionStringBuilder.NoResetOnClose"/> is set.
/// </para>
/// </remarks>
internal sealed class PostgresSessionLock : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly long _key;
    private readonly UnlockFunc _unlock;
    private int _released;

    /// <summary>Releases an advisory lock that a connection may hold.</summary>
    /// <param name="connection">The connection that took the lock.</param>
    /// <param name="key">The advisory-lock key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when the connection held the lock and released it.</returns>
    internal delegate Task<bool> UnlockFunc(NpgsqlConnection connection, long key, CancellationToken cancellationToken);

    /// <summary>Wraps a connection that already holds the advisory lock for <paramref name="key"/>.</summary>
    /// <param name="connection">The dedicated, unpooled connection holding the lock. Owned by this instance from now on.</param>
    /// <param name="key">The advisory-lock key that the connection holds.</param>
    internal PostgresSessionLock(NpgsqlConnection connection, long key)
        : this(connection, key, UnlockAsync)
    {
    }

    /// <summary>Wraps a connection that already holds the advisory lock, with the unlock step supplied by the caller.</summary>
    /// <param name="connection">The dedicated, unpooled connection holding the lock. Owned by this instance from now on.</param>
    /// <param name="key">The advisory-lock key that the connection holds.</param>
    /// <param name="unlock">The unlock step. <see cref="UnlockAsync"/> in production; a test supplies a failing one.</param>
    internal PostgresSessionLock(NpgsqlConnection connection, long key, UnlockFunc unlock)
    {
        _connection = connection;
        _key = key;
        _unlock = unlock;
    }

    /// <summary>
    /// Releases the advisory lock, then closes the connection, which ends its backend. When the unlock fails, the connection
    /// is still closed and the failure propagates. A second call does nothing.
    /// </summary>
    /// <returns>A task that completes when the lock is released and the connection is closed.</returns>
    /// <exception cref="InvalidOperationException">The connection did not hold the lock when it was released.</exception>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        bool released;
        try
        {
            released = await _unlock(_connection, _key, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // The connection may still hold the lock. Closing it ends the backend, which releases the lock on the server.
            await CloseConnectionAsync(_connection).ConfigureAwait(false);
            throw;
        }

        await CloseConnectionAsync(_connection).ConfigureAwait(false);
        if (!released)
        {
            throw new InvalidOperationException("The session advisory lock was not held when it was released.");
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

    /// <summary>
    /// Closes a connection that is not pooled. Closing sends Terminate and closes the socket, so the server backend exits and
    /// releases its session advisory locks.
    /// </summary>
    /// <param name="connection">The unpooled connection to close.</param>
    /// <returns>A task that completes when the connection is closed.</returns>
    internal static async Task CloseConnectionAsync(NpgsqlConnection connection)
    {
        await connection.CloseAsync().ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }
}
