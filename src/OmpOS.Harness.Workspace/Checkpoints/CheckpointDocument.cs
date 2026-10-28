namespace OmpOS.Workspace.Checkpoints;

/// <summary>The persisted timeline of one session. <see cref="Version"/> changes when the shape does; a file with another version is read as an empty timeline.</summary>
internal sealed class CheckpointDocument
{
    public int Version { get; set; }

    public string SessionId { get; set; } = string.Empty;

    public int NextTurnId { get; set; } = 1;

    public List<CheckpointTurnDocument> Turns { get; set; } = [];

    public List<string> RewoundKeys { get; set; } = [];
}
