namespace OmpOS.Presentation.Checkpoints;

/// <summary>The files one agent turn changed, read from the transcript.</summary>
/// <param name="TurnKey">A stable key of the turn: the id of the user message that began it, or <c>session-start</c> for edits before any user message.</param>
/// <param name="Label">The prompt of the turn, shortened.</param>
/// <param name="Files">The files, in the order the turn first edited them.</param>
public sealed record CheckpointTurnEdits(string TurnKey, string Label, IReadOnlyList<CheckpointFileEdit> Files);
