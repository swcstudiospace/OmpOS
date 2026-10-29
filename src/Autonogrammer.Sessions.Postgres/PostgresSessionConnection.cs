using Autonogrammer.Resilience;
using Npgsql;

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
    /// <summary>The search path of every store connection. Statements name tables without a schema and resolve them in <c>autonogrammer</c> first.</summary>
    internal const string SearchPath = "autonogrammer,public";

    private const int MigratorMaxPoolSize = 2;
    private const int ReadOnlyMaxPoolSize = 4;

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

    /// <summary>
    /// Creates the data source of one role from this connection string. The data source uses the search path <c>autonogrammer,public</c>, whatever search path the
    /// string carries. A connect timeout and a pool size are set from <paramref name="options"/> and the role unless the string already sets them: the runtime
    /// role gets <c>Maximum Pool Size</c> of the interactive plus the background share (16 + 4 = 20), the migration role 2 and the read-only role 4.
    /// </summary>
    /// <param name="role">The role the connection string belongs to.</param>
    /// <param name="options">The PostgreSQL resilience options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A new data source. The caller owns it and disposes it.</returns>
    public NpgsqlDataSource CreateDataSource(PostgresRole role = PostgresRole.Runtime, PostgresResilienceOptions? options = null)
    {
        options ??= new PostgresResilienceOptions();
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { SearchPath = SearchPath };
        if (!builder.ContainsKey("Timeout"))
        {
            builder.Timeout = options.ConnectSeconds;
        }

        if (!builder.ContainsKey("Maximum Pool Size"))
        {
            builder.MaxPoolSize = role switch
            {
                PostgresRole.Migrator => MigratorMaxPoolSize,
                PostgresRole.ReadOnly => ReadOnlyMaxPoolSize,
                _ => options.PoolInteractive + options.PoolBackground,
            };
        }

        return NpgsqlDataSource.Create(builder.ConnectionString);
    }

    /// <summary>Returns a description that does not reveal the connection string.</summary>
    /// <returns>A fixed description.</returns>
    public override string ToString() => "PostgresSessionConnection (connection string not shown)";
}
