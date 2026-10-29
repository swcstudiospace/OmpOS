namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// An applied migration whose recorded checksum differs from the checksum of the step compiled into this build: the step's SQL changed after it was
/// applied. The runner stops without applying anything; a step that shipped is never edited, a new step changes it.
/// </summary>
public sealed class MigrationChecksumMismatchException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MigrationChecksumMismatchException"/> class.</summary>
    public MigrationChecksumMismatchException()
        : this(0, "unknown", string.Empty, string.Empty)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MigrationChecksumMismatchException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public MigrationChecksumMismatchException(string message)
        : base(message)
    {
        MigrationName = "unknown";
        RecordedChecksum = string.Empty;
        CompiledChecksum = string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="MigrationChecksumMismatchException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public MigrationChecksumMismatchException(string message, Exception? innerException)
        : base(message, innerException)
    {
        MigrationName = "unknown";
        RecordedChecksum = string.Empty;
        CompiledChecksum = string.Empty;
    }

    /// <summary>Initializes a new instance of the <see cref="MigrationChecksumMismatchException"/> class.</summary>
    /// <param name="version">The version of the step.</param>
    /// <param name="migrationName">The name of the step.</param>
    /// <param name="recordedChecksum">The checksum stored when the step was applied.</param>
    /// <param name="compiledChecksum">The checksum of the step in this build.</param>
    public MigrationChecksumMismatchException(int version, string migrationName, string recordedChecksum, string compiledChecksum)
        : base($"Migration {version} ({migrationName}) was applied with checksum {recordedChecksum}, but this build carries checksum {compiledChecksum}: the step changed after it was applied.")
    {
        Version = version;
        MigrationName = migrationName;
        RecordedChecksum = recordedChecksum;
        CompiledChecksum = compiledChecksum;
    }

    /// <summary>Gets the version of the step whose checksum differs.</summary>
    public int Version { get; }

    /// <summary>Gets the name of the step whose checksum differs.</summary>
    public string MigrationName { get; }

    /// <summary>Gets the checksum stored in <c>schema_migrations</c>.</summary>
    public string RecordedChecksum { get; }

    /// <summary>Gets the checksum of the step compiled into this build.</summary>
    public string CompiledChecksum { get; }
}
