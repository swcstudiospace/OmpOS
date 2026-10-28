namespace OmpOS.Native.Views.Checkpoints;

/// <summary>
/// The labels and accessible names of the checkpoint timeline that <c>CheckpointsCopy</c> does not hold. The title, the empty sentence, the Rewind, Preview, Overwrite
/// and Cancel labels, the count, the row names and the status sentences come from the model; the view reads these constants for the rest and never writes a literal.
/// </summary>
internal static class CheckpointsViewText
{
    /// <summary>The label of the Refresh button.</summary>
    public const string RefreshLabel = "Refresh";

    /// <summary>The accessible name of the Refresh button.</summary>
    public const string RefreshName = "Refresh checkpoints";

    /// <summary>The label of the button that rewinds the latest checkpoint.</summary>
    public const string UndoLastLabel = "Undo last turn";

    /// <summary>The accessible name of the Undo last turn button.</summary>
    public const string UndoLastName = "Undo last turn: rewind files to before the latest checkpoint";

    /// <summary>The label of the Close button.</summary>
    public const string CloseLabel = "Close";

    /// <summary>The accessible name of the Close button.</summary>
    public const string CloseName = "Close checkpoints";

    /// <summary>The accessible name of the list of turns.</summary>
    public const string RowsName = "Checkpoint turns";

    /// <summary>The accessible name of the confirmation block.</summary>
    public const string ConfirmationName = "Rewind confirmation";

    /// <summary>The accessible name of the list of files a rewind would change.</summary>
    public const string PreviewListName = "Files a rewind would change";

    /// <summary>The accessible name of the button that confirms a rewind.</summary>
    public const string ConfirmName = "Confirm rewind";

    /// <summary>The accessible name of the button that dismisses a confirmation.</summary>
    public const string CancelName = "Cancel rewind";

    /// <summary>The accessible name of the status line, a live region.</summary>
    public const string StatusName = "Checkpoints status";
}
