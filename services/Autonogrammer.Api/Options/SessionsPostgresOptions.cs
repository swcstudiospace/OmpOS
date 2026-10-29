namespace Autonogrammer.Api.Options;

/// <summary>
/// Binds the <c>Sessions:Postgres</c> configuration section. Startup validation requires <see cref="ConnectionString"/> when
/// <c>Sessions:Store</c> is <c>Postgres</c>; the value is never logged.
/// </summary>
internal sealed class SessionsPostgresOptions
{
    /// <summary>Gets or sets the Npgsql connection string. Required when <c>Sessions:Store</c> is <c>Postgres</c>.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Npgsql connection string of the migration role. Only the one-shot migrate service sets it; the API process never needs it. The value is
    /// never logged.
    /// </summary>
    public string? MigratorConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the API runs the migration runner at startup with the runtime connection string. Development and tests only;
    /// the default is <see langword="false"/>, and the schema is then migrated by the migrate service.
    /// </summary>
    public bool AutoMigrate { get; set; }
}
