using System.Globalization;

namespace OmpOS.Presentation.Changes;

/// <summary>The words of the review of a session's file changes: control names, status text and the sentences read out after an action.</summary>
public static class ChangeCopy
{
    /// <summary>The accessible name of the Changes bar.</summary>
    public const string BarName = "Changes made by the agent";

    /// <summary>The accessible name of the list of changed files.</summary>
    public const string ListName = "Changed files";

    /// <summary>The label of the control that keeps every pending change.</summary>
    public const string KeepAll = "Keep all";

    /// <summary>The accessible name of the control that keeps every pending change.</summary>
    public const string KeepAllName = "Keep all changes";

    /// <summary>The label of the control that undoes every pending change.</summary>
    public const string UndoAll = "Undo all";

    /// <summary>The accessible name of the control that undoes every pending change.</summary>
    public const string UndoAllName = "Undo all changes";

    /// <summary>The label of the control that keeps the changes to one file.</summary>
    public const string Keep = "Keep";

    /// <summary>The label of the control that undoes the changes to one file.</summary>
    public const string Undo = "Undo";

    /// <summary>The label of the control that opens the review of one file.</summary>
    public const string Review = "Review";

    /// <summary>The accessible name of the line that announces the result of the last action.</summary>
    public const string ResultName = "Change review result";

    /// <summary>The sentence a refused undo of a file that changed after the agent edited it says.</summary>
    public const string StaleMessage = "The file changed since the agent edited it; review it by hand.";

    /// <summary>The sentence the Diff review tool shows while no file is under review.</summary>
    public const string NothingToReview = "No changes to review.";

    /// <summary>The label of the control that writes the reviewed hunks to the file.</summary>
    public const string ApplyReview = "Apply review";

    /// <summary>The accessible name of the control that writes the reviewed hunks to the file.</summary>
    public const string ApplyReviewName = "Apply the reviewed hunks to the file";

    /// <summary>Gets the status text of a file's edits.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The status text.</returns>
    public static string StatusText(ChangeStatus status) => status switch
    {
        ChangeStatus.Kept => "Kept",
        ChangeStatus.Undone => "Undone",
        ChangeStatus.Stale => "Changed since",
        _ => "Pending",
    };

    /// <summary>Gets the word for what the edits did to a file.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The word.</returns>
    public static string KindText(ChangeKind kind) => kind switch
    {
        ChangeKind.Created => "Created",
        ChangeKind.Emptied => "Emptied",
        _ => "Edited",
    };

    /// <summary>Gets the count of added lines, such as <c>+3</c>.</summary>
    /// <param name="added">The number of added lines.</param>
    /// <returns>The text.</returns>
    public static string AddedText(int added) => string.Create(CultureInfo.InvariantCulture, $"+{added}");

    /// <summary>Gets the count of removed lines, such as <c>-1</c>.</summary>
    /// <param name="removed">The number of removed lines.</param>
    /// <returns>The text.</returns>
    public static string RemovedText(int removed) => string.Create(CultureInfo.InvariantCulture, $"-{removed}");

    /// <summary>Gets the summary line of the Changes bar.</summary>
    /// <param name="files">The number of changed files.</param>
    /// <param name="added">The number of added lines in all of them.</param>
    /// <param name="removed">The number of removed lines in all of them.</param>
    /// <returns>The text, such as <c>2 files changed +10 -3</c>.</returns>
    public static string Summary(int files, int added, int removed) =>
        string.Create(CultureInfo.InvariantCulture, $"{files} {(files == 1 ? "file" : "files")} changed {AddedText(added)} {RemovedText(removed)}");

    /// <summary>Gets the accessible name of the control that keeps one file's changes.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The name.</returns>
    public static string KeepName(string path) => $"Keep changes to {path}";

    /// <summary>Gets the accessible name of the control that undoes one file's changes.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The name.</returns>
    public static string UndoName(string path) => $"Undo changes to {path}";

    /// <summary>Gets the accessible name of the control that opens the review of one file.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The name.</returns>
    public static string ReviewName(string path) => $"Review changes to {path}";

    /// <summary>Gets the accessible name of one file's row.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <param name="kind">What the edits did to the file.</param>
    /// <param name="status">Where the file's review stands.</param>
    /// <param name="added">The number of added lines.</param>
    /// <param name="removed">The number of removed lines.</param>
    /// <returns>The name.</returns>
    public static string RowName(string path, ChangeKind kind, ChangeStatus status, int added, int removed) =>
        string.Create(CultureInfo.InvariantCulture, $"{path}, {KindText(kind)}, {added} added, {removed} removed, {StatusText(status)}");

    /// <summary>Gets the title of the Diff review tool while it shows one file's changes.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The title.</returns>
    public static string ReviewTitle(string path) => $"Reviewing the agent's changes to {path}";

    /// <summary>Gets the sentence that says the changes to a file were kept.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The sentence.</returns>
    public static string KeptMessage(string path) => $"Kept the changes to {path}.";

    /// <summary>Gets the sentence that says the changes to a file were undone.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The sentence.</returns>
    public static string UndoneMessage(string path) => $"Undid the agent's changes to {path}.";

    /// <summary>Gets the sentence that says a file the agent created was removed.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The sentence.</returns>
    public static string RemovedMessage(string path) => $"Removed {path}, which the agent created.";

    /// <summary>Gets the sentence that says a file the agent created is already gone.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The sentence.</returns>
    public static string AlreadyGoneMessage(string path) => $"{path} is already gone, so there is nothing to undo.";

    /// <summary>Gets the sentence that says only some hunks of a file were undone.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <param name="undone">The number of hunks undone.</param>
    /// <param name="total">The number of hunks of the review.</param>
    /// <returns>The sentence.</returns>
    public static string PartlyUndoneMessage(string path, int undone, int total) =>
        string.Create(CultureInfo.InvariantCulture, $"Undid {undone} of {total} hunks in {path}.");

    /// <summary>Gets the sentence that says a file changed after the agent edited it, naming the file.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The sentence.</returns>
    public static string StaleAnnouncement(string path) => $"{path}: {StaleMessage}";

    /// <summary>Gets the sentence that says a path is outside the workspace.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The sentence.</returns>
    public static string RefusedMessage(string path) => $"{path} is outside the workspace, so nothing was changed.";

    /// <summary>Gets the sentence that says a file could not be changed.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <param name="reason">The reason the file system gave.</param>
    /// <returns>The sentence.</returns>
    public static string FailedMessage(string path, string reason) => $"Could not change {path}. {reason}";

    /// <summary>Gets the sentence that says no pending change has the path.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The sentence.</returns>
    public static string NotPendingMessage(string path) => $"{path} has no change waiting for a decision.";

    /// <summary>Gets the sentence read out after Keep all.</summary>
    /// <param name="kept">The number of files kept.</param>
    /// <returns>The sentence.</returns>
    public static string KeptAllMessage(int kept) =>
        string.Create(CultureInfo.InvariantCulture, $"Kept the changes to {kept} {(kept == 1 ? "file" : "files")}.");

    /// <summary>Gets the sentence read out after Undo all.</summary>
    /// <param name="undone">The number of files restored or removed.</param>
    /// <param name="left">The number of files left as they are, because they changed since the agent edited them or could not be written.</param>
    /// <returns>The sentence.</returns>
    public static string UndoneAllMessage(int undone, int left)
    {
        var done = string.Create(CultureInfo.InvariantCulture, $"Undid the changes to {undone} {(undone == 1 ? "file" : "files")}.");
        return left switch
        {
            0 => done,
            1 => done + " 1 file was left as it is; review it by hand.",
            _ => string.Create(CultureInfo.InvariantCulture, $"{done} {left} files were left as they are; review them by hand."),
        };
    }
}
