namespace OmpOS.Workspace.Checkpoints;

/// <summary>The persisted form of one <see cref="CheckpointTurn"/>.</summary>
internal sealed class CheckpointTurnDocument
{
    public int TurnId { get; set; }

    public string TurnKey { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public DateTimeOffset RecordedAt { get; set; }

    public List<CheckpointFileDocument> Files { get; set; } = [];
}
