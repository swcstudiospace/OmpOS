namespace Autonogrammer.Api.DataPlane;

/// <summary>Where the PostgreSQL schema stands against the compiled migration head.</summary>
internal enum SchemaState
{
    /// <summary>The session store is not PostgreSQL, so there is no schema to compare.</summary>
    NotApplicable,

    /// <summary>The schema is older than the compiled head; a migration is pending.</summary>
    Behind,

    /// <summary>The schema is at the compiled head.</summary>
    Current,

    /// <summary>The schema is newer than the compiled head. This build is older than the database; it keeps running.</summary>
    Ahead,
}
