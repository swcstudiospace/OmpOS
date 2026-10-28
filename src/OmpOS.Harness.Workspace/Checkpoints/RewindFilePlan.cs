namespace OmpOS.Workspace.Checkpoints;

/// <summary>What a rewind does to one file.</summary>
/// <param name="Root">The absolute real path of the workspace folder.</param>
/// <param name="Path">The path relative to <paramref name="Root"/>, with forward slashes.</param>
/// <param name="Action">What happens to the file.</param>
/// <param name="Deletes"><see langword="true"/> when the rewind removes the file (the turn created it). For a <see cref="RewindFileAction.Stale"/> file it says what a forced rewind would do.</param>
/// <param name="Detail">One sentence that says why, for the status line and assistive technology.</param>
public sealed record RewindFilePlan(string Root, string Path, RewindFileAction Action, bool Deletes, string Detail);
