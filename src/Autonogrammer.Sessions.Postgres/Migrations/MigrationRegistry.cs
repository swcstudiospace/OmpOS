using System.Collections.Immutable;

namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// The compiled list of migration steps, in version order. The last entry is the head the <see cref="MigrationRunner"/> migrates to and the API waits for.
/// A later phase adds its step as one new file and one new line at the end of <see cref="All"/>; a step that shipped is never edited or reordered.
/// </summary>
public static class MigrationRegistry
{
    /// <summary>Gets every step, versions strictly increasing and unique.</summary>
    public static readonly ImmutableArray<IMigrationStep> All =
    [
        new M0001_SessionStore(),
        new M0002_CloudAgents(),
        new M0003_SessionSurfaceOwner(),
        new M0004_Accounts(),
        new M0005_Dispatch(),
        new M0006_Cowork(),
        new M0007_LedgerOutbox(),
        new M0008_Retention(),
        new M0009_CoworkProjectsMemory(),
        new M0010_DispatchDeviceState(),
        new M0011_DispatchSweepIndexes(),
    ];

    /// <summary>Gets the version of the last step: the schema version this build expects.</summary>
    public static int Head => All[^1].Version;
}
