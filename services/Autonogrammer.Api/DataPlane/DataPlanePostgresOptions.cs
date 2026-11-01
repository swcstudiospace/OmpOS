namespace Autonogrammer.Api.DataPlane;

/// <summary>Binds <c>DataPlane:Postgres</c>.</summary>
internal sealed class DataPlanePostgresOptions
{
    /// <summary>Gets the smallest accepted schema wait.</summary>
    public const int MinSchemaWaitSeconds = 1;

    /// <summary>Gets the largest accepted schema wait.</summary>
    public const int MaxSchemaWaitSeconds = 600;

    /// <summary>Gets the configuration key of the schema wait.</summary>
    public const string SchemaWaitSecondsKey = "DataPlane:Postgres:SchemaWaitSeconds";

    /// <summary>
    /// Gets or sets how long the API waits for the schema to reach the compiled head before it stops and names
    /// <c>Sessions:Postgres:MigratorConnectionString</c>. Valid from 1 to 600; the default is 120.
    /// </summary>
    public int SchemaWaitSeconds { get; set; } = 120;
}
