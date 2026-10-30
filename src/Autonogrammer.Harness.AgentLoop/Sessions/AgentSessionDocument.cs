namespace Autonogrammer.Harness.AgentLoop.Sessions;

/// <summary>The versioned state file of the session registry.</summary>
internal sealed class AgentSessionDocument
{
    /// <summary>Gets or sets the schema version. A file with a version other than the current one is quarantined.</summary>
    public int SchemaVersion { get; set; }

    /// <summary>Gets or sets the sessions. A null list makes the file invalid.</summary>
    public List<AgentSessionEntry>? Sessions { get; set; }
}
