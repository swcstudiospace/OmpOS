using System.Globalization;

namespace Autonogrammer.Presentation.Worktrees;

/// <summary>The words of the worktree commands.</summary>
public static class WorktreeCommandsCopy
{
    /// <summary>The most candidates <c>/best-of-n</c> makes.</summary>
    public const int MaxBestOf = 8;

    /// <summary>The sentence shown while there is no run worktree.</summary>
    public const string Empty = "No worktrees. /worktree starts a run in its own checkout.";

    /// <summary>The label of the control that starts a worktree run.</summary>
    public const string StartLabel = "New worktree";

    /// <summary>The label of the control that shows a run's patch.</summary>
    public const string PreviewLabel = "Review changes";

    /// <summary>The label of the control that applies a run's changes.</summary>
    public const string ApplyLabel = "Apply to this checkout";

    /// <summary>The label of the control that merges a run's changes three ways.</summary>
    public const string ApplyThreeWayLabel = "Merge three ways";

    /// <summary>The label of the control that deletes a run.</summary>
    public const string DeleteLabel = "Delete worktree";

    /// <summary>The label of the control that confirms a deletion.</summary>
    public const string ConfirmDeleteLabel = "Delete";

    /// <summary>The label of the control that dismisses a confirmation.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>The status line when a deletion is dismissed.</summary>
    public const string DeleteCancelled = "Nothing was deleted.";

    /// <summary>Gets the text of a run's row.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The text.</returns>
    public static string RunRow(WorktreeRunInfo run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var changes = run.Problem is null
            ? string.Create(CultureInfo.InvariantCulture, $"{run.ChangedFiles} {(run.ChangedFiles == 1 ? "file" : "files")} changed")
            : "changes unknown: " + run.Problem.TrimEnd('.');
        return string.Create(CultureInfo.InvariantCulture, $"{run.RunId} on {run.Branch}, {changes}");
    }

    /// <summary>Gets the accessible name of a run's Preview control.</summary>
    /// <param name="runId">The run.</param>
    /// <returns>The name.</returns>
    public static string PreviewName(string runId) => "Review the changes of worktree " + runId;

    /// <summary>Gets the accessible name of a run's Apply control.</summary>
    /// <param name="runId">The run.</param>
    /// <returns>The name.</returns>
    public static string ApplyName(string runId) => "Apply the changes of worktree " + runId + " to this checkout";

    /// <summary>Gets the accessible name of a run's Delete control.</summary>
    /// <param name="runId">The run.</param>
    /// <returns>The name.</returns>
    public static string DeleteName(string runId) => "Delete worktree " + runId;

    /// <summary>Gets the sentence for a started run.</summary>
    /// <param name="run">The run.</param>
    /// <param name="setupMessage">The sentence about its setup, or an empty string.</param>
    /// <returns>The sentence.</returns>
    public static string Started(WorktreeRunInfo run, string setupMessage)
    {
        ArgumentNullException.ThrowIfNull(run);
        var text = "Started worktree " + run.RunId + " on branch " + run.Branch + ".";
        return string.IsNullOrWhiteSpace(setupMessage) ? text : text + " " + setupMessage;
    }

    /// <summary>Gets the sentence for a patch preview.</summary>
    /// <param name="preview">The patch.</param>
    /// <returns>The sentence.</returns>
    public static string PatchSummary(WorktreePatchPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return preview.Files.Count == 0
            ? "Worktree " + preview.RunId + " changed no files."
            : string.Create(CultureInfo.InvariantCulture, $"Worktree {preview.RunId} changed {preview.Files.Count} {(preview.Files.Count == 1 ? "file" : "files")}: +{preview.Added} -{preview.Removed}.");
    }

    /// <summary>Gets the question a deletion asks.</summary>
    /// <param name="runs">The runs that would be deleted, with their changes.</param>
    /// <returns>The question. It says that changes that were not applied are lost.</returns>
    public static string DeleteConfirmation(IReadOnlyList<WorktreeRunInfo> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var files = runs.Sum(static run => run.ChangedFiles);
        var names = string.Join(", ", runs.Select(static run => run.RunId));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Delete {(runs.Count == 1 ? "worktree" : "worktrees")} {names}? {files} changed {(files == 1 ? "file is" : "files are")} lost unless you applied {(runs.Count == 1 ? "it" : "them")} first.");
    }

    /// <summary>Gets the sentence for a finished deletion.</summary>
    /// <param name="count">The number of runs deleted.</param>
    /// <returns>The sentence.</returns>
    public static string Deleted(int count) => string.Create(CultureInfo.InvariantCulture, $"Deleted {count} {(count == 1 ? "worktree" : "worktrees")}.");

    /// <summary>Gets the row text of a best-of-n candidate.</summary>
    /// <param name="index">The candidate's number.</param>
    /// <param name="state">Where it stands.</param>
    /// <param name="changedFiles">The files it changed.</param>
    /// <param name="summary">What the agent said.</param>
    /// <returns>The text.</returns>
    public static string CandidateRow(int index, BestOfNState state, int changedFiles, string summary)
    {
        var stateText = state switch
        {
            BestOfNState.Waiting => "waiting",
            BestOfNState.Running => "running",
            BestOfNState.Finished => "finished",
            _ => "failed",
        };
        var text = string.Create(CultureInfo.InvariantCulture, $"Candidate {index}: {stateText}, {changedFiles} {(changedFiles == 1 ? "file" : "files")} changed");
        return string.IsNullOrWhiteSpace(summary) ? text : text + ". " + summary.Trim();
    }

    /// <summary>Gets the sentence for a failure.</summary>
    /// <param name="reason">The failure.</param>
    /// <returns>The sentence.</returns>
    public static string Failed(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return "Worktree command failed: " + reason.TrimEnd('.') + ".";
    }

    /// <summary>Gets the sentence for a count of candidates out of range.</summary>
    /// <returns>The sentence.</returns>
    public static string CountOutOfRange() => string.Create(CultureInfo.InvariantCulture, $"Choose between 1 and {MaxBestOf} candidates.");

    /// <summary>Gets the sentence for a best-of-n run that was asked without a prompt.</summary>
    /// <returns>The sentence.</returns>
    public static string NeedsPrompt() => "Type the task after /best-of-n, for example /best-of-n 3 fix the failing test.";

    /// <summary>Gets the sentence for a head that cannot run candidates.</summary>
    /// <returns>The sentence.</returns>
    public static string NoBestOfHost() => "This window cannot run agents in worktrees yet.";

    /// <summary>Gets the sentence for a finished best-of-n run.</summary>
    /// <param name="finished">How many candidates finished.</param>
    /// <param name="count">How many there are.</param>
    /// <returns>The sentence.</returns>
    public static string BestOfDone(int finished, int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{finished} of {count} candidates finished. Review each one, then apply the best and discard the rest.");
}
