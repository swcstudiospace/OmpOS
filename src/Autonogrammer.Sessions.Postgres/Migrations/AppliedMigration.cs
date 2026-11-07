namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>One row of <c>schema_migrations</c>: a step that has been applied.</summary>
/// <param name="Version">The step's version.</param>
/// <param name="Name">The step's name.</param>
/// <param name="Checksum">The SHA-256 of the step's up SQL when it was applied, lower-case hexadecimal.</param>
public sealed record AppliedMigration(int Version, string Name, string Checksum);
