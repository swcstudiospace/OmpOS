namespace OmpOS.Presentation.Checkpoints;

/// <summary>One turn of the checkpoint timeline.</summary>
/// <param name="TurnId">The turn's number in the timeline.</param>
/// <param name="Label">The prompt of the turn, or an empty string.</param>
/// <param name="RecordedAt">When the turn was first recorded.</param>
/// <param name="Files">The files the turn changed, relative to their workspace folders.</param>
public sealed record CheckpointTurnInfo(int TurnId, string Label, DateTimeOffset RecordedAt, IReadOnlyList<string> Files);
