namespace Autonogrammer.Presentation.Editor;

/// <summary>The outcome of writing a reviewed diff to disk with <see cref="DiffReviewWriter"/>.</summary>
public enum DiffWriteStatus
{
    /// <summary>The accepted hunks were written to the file.</summary>
    Written,

    /// <summary>No hunk is accepted, so the result equals the original and the file was not touched.</summary>
    Unchanged,

    /// <summary>The file on disk no longer holds the text the diff was reviewed against. Nothing was written.</summary>
    Stale,
}
