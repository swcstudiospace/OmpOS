using System.Globalization;

namespace OmpOS.Presentation.Checkpoints;

/// <summary>The words of the checkpoint timeline. The sentences say what a rewind does and does not do, so nobody expects it to change the conversation.</summary>
public static class CheckpointsCopy
{
    /// <summary>The title of the checkpoint timeline.</summary>
    public const string Title = "Checkpoints";

    /// <summary>The sentence shown while the session has no checkpoint.</summary>
    public const string Empty = "No checkpoints yet. One is kept for every turn in which the agent changes a file.";

    /// <summary>The sentence that says what a rewind leaves alone.</summary>
    public const string ConversationUnchanged = "Only files are restored. The conversation is not changed.";

    /// <summary>The label of the control that restores the files.</summary>
    public const string RewindLabel = "Rewind";

    /// <summary>The label of the control that shows what a rewind would do.</summary>
    public const string PreviewLabel = "Preview";

    /// <summary>The label of the control that overwrites changed files too.</summary>
    public const string ForceLabel = "Overwrite changed files";

    /// <summary>The label of the control that dismisses a confirmation.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>The status line while the timeline loads.</summary>
    public const string Loading = "Loading checkpoints.";

    /// <summary>The status line when the confirmation is dismissed.</summary>
    public const string Cancelled = "Rewind cancelled. Nothing was changed.";

    /// <summary>The status line when the chosen checkpoint no longer exists.</summary>
    public const string Gone = "That checkpoint no longer exists.";

    /// <summary>Gets the sentence for a checkpoint count.</summary>
    /// <param name="count">The number of checkpoints.</param>
    /// <returns>The sentence.</returns>
    public static string Count(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "checkpoint" : "checkpoints")}");

    /// <summary>Gets the accessible name of a timeline row.</summary>
    /// <param name="number">The turn's position in the timeline, from 1.</param>
    /// <param name="label">The turn's prompt, or an empty string.</param>
    /// <param name="files">The number of files the turn changed.</param>
    /// <param name="time">The relative time.</param>
    /// <returns>The name.</returns>
    public static string RowName(int number, string label, int files, string time)
    {
        ArgumentNullException.ThrowIfNull(label);

        return string.Create(CultureInfo.InvariantCulture, $"Turn {number}{(label.Length > 0 ? ": " + label : string.Empty)}, {files} {(files == 1 ? "file" : "files")} changed, {time}");
    }

    /// <summary>Gets the accessible name of a row's Preview control.</summary>
    /// <param name="number">The turn's position.</param>
    /// <returns>The name.</returns>
    public static string PreviewName(int number) => string.Create(CultureInfo.InvariantCulture, $"Preview rewinding to before turn {number}");

    /// <summary>Gets the accessible name of a row's Rewind control.</summary>
    /// <param name="number">The turn's position.</param>
    /// <returns>The name.</returns>
    public static string RewindName(int number) => string.Create(CultureInfo.InvariantCulture, $"Rewind files to before turn {number}");

    /// <summary>Gets the question a confirmation asks.</summary>
    /// <param name="number">The turn's position.</param>
    /// <param name="preview">What the rewind would do.</param>
    /// <returns>The question, with the counts and the reminder that the conversation stays.</returns>
    public static string Confirmation(int number, CheckpointRewindPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"Rewind to before turn {number}? {preview.ChangeCount} {(preview.ChangeCount == 1 ? "file is" : "files are")} written or deleted.");
        if (preview.StaleCount > 0)
        {
            text += string.Create(
                CultureInfo.InvariantCulture,
                $" {preview.StaleCount} {(preview.StaleCount == 1 ? "file has" : "files have")} changed since the agent's edit; nothing is written unless you overwrite them.");
        }

        if (preview.SkippedCount > 0)
        {
            text += string.Create(CultureInfo.InvariantCulture, $" {preview.SkippedCount} cannot be restored and stay as they are.");
        }

        return text + " " + ConversationUnchanged;
    }

    /// <summary>Gets the sentence for a preview.</summary>
    /// <param name="number">The turn's position.</param>
    /// <param name="preview">What the rewind would do.</param>
    /// <returns>The sentence.</returns>
    public static string PreviewSummary(int number, CheckpointRewindPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return preview.Files.Count == 0
            ? string.Create(CultureInfo.InvariantCulture, $"Turn {number}: nothing to preview.")
            : string.Create(CultureInfo.InvariantCulture, $"Rewinding to before turn {number} touches {preview.ChangeCount} {(preview.ChangeCount == 1 ? "file" : "files")}, {preview.StaleCount} changed since the agent's edit, {preview.SkippedCount} left alone.");
    }

    /// <summary>Gets the sentence for a failure of the backend.</summary>
    /// <param name="reason">The failure.</param>
    /// <returns>The sentence.</returns>
    public static string Failed(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return "Checkpoints failed: " + reason.TrimEnd('.') + ".";
    }
}
