namespace Autonogrammer.Presentation.Checkpoints;

/// <summary>The result of a rewind.</summary>
/// <param name="Status">How it ended.</param>
/// <param name="Preview">The plan with the state of the files when the rewind started.</param>
/// <param name="Message">One sentence that says what happened.</param>
public sealed record CheckpointRewindOutcome(CheckpointRewindStatus Status, CheckpointRewindPreview Preview, string Message);
