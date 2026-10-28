namespace OmpOS.Presentation.Checkpoints;

/// <summary>One file of a rewind preview.</summary>
/// <param name="Path">The path relative to its workspace folder.</param>
/// <param name="State">What the rewind does to it.</param>
/// <param name="Detail">One sentence that says why.</param>
public sealed record CheckpointFilePreview(string Path, CheckpointFileState State, string Detail);
