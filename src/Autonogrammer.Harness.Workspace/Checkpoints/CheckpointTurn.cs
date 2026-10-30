namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>The files one agent turn changed, as the store kept them.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="TurnId">The number of the turn in the session's timeline. It starts at 1, only grows and is never reused, even after a rewind.</param>
/// <param name="TurnKey">The key the recorder gave the turn.</param>
/// <param name="Label">A short description of the turn, or an empty string.</param>
/// <param name="RecordedAt">When the turn was first recorded.</param>
/// <param name="Files">The changed files, in the order the turn first changed them.</param>
public sealed record CheckpointTurn(string SessionId, int TurnId, string TurnKey, string Label, DateTimeOffset RecordedAt, IReadOnlyList<CheckpointFileEntry> Files);
