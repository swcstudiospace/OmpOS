namespace Autonogrammer.Native.Composition.ComputerUse;

/// <summary>JSON file the store reads and writes. Schema version 1.</summary>
internal sealed class ComputerUseSettingsFile
{
    /// <summary>Schema version.</summary>
    public int SchemaVersion { get; set; } = FileComputerUseSettingsStore.CurrentSchemaVersion;

    /// <summary>Kill chord override.</summary>
    public string? KillChord { get; set; }

    /// <summary>Security phrase.</summary>
    public string? SecurityPhrase { get; set; }

    /// <summary>Audit thumbnails. Default off.</summary>
    public bool? AuditThumbnails { get; set; }

    /// <summary>User denylist additions.</summary>
    public List<string> DenylistAdditions { get; set; } = [];
}
