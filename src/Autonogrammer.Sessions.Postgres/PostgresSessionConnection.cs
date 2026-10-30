namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// The connection string the PostgreSQL session store was configured with, kept verbatim. Session locks connect with it
/// directly. <see cref="Npgsql.NpgsqlDataSource.ConnectionString"/> is not used for that, because it does not carry every
/// setting, the password among them.
/// </summary>
/// <remarks>
/// The value holds credentials. It is never logged: <see cref="ToString"/> does not show it.
/// </remarks>
public sealed class PostgresSessionConnection
{
    /// <summary>Creates the holder for a configured connection string.</summary>
    /// <param name="connectionString">The non-blank connection string, as configured.</param>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is null, empty or white space.</exception>
    public PostgresSessionConnection(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
    }

    /// <summary>Gets the connection string, verbatim. It contains credentials: never log it.</summary>
    public string ConnectionString { get; }

    /// <summary>Returns a description that does not reveal the connection string.</summary>
    /// <returns>A fixed description.</returns>
    public override string ToString() => "PostgresSessionConnection (connection string not shown)";
}
