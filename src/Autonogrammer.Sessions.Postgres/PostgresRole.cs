namespace Autonogrammer.Sessions.Postgres;

/// <summary>The database role a data source connects as. Each role has its own connection string and its own pool, so the migration role's secret never sits in the API process.</summary>
public enum PostgresRole
{
    /// <summary>The runtime role <c>autonogrammer_app</c>: data manipulation only, no schema changes, insert and select only on the append-only audit table.</summary>
    Runtime,

    /// <summary>The migration role <c>autonogrammer_migrator</c>: owns the schema objects and runs the migrations. Only the one-shot migrate service holds its connection string.</summary>
    Migrator,

    /// <summary>The optional support role <c>autonogrammer_ro</c>: read only.</summary>
    ReadOnly,
}
