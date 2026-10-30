using System.Globalization;

namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>The sentences the checkpoint store reports.</summary>
internal static class CheckpointCopy
{
    /// <summary>Gets the sentence for a rewind that has nothing to write.</summary>
    public static string NothingToDo => "Nothing to restore: the files already hold their earlier content, or cannot be restored.";

    /// <summary>Tells that a rewind wrote nothing because files changed since the agent's edit.</summary>
    /// <param name="stale">The number of files that changed.</param>
    /// <returns>The sentence.</returns>
    public static string Blocked(int stale) =>
        string.Create(CultureInfo.InvariantCulture, $"{stale} {(stale == 1 ? "file has" : "files have")} changed since the agent's edit, so nothing was restored. Review them, or force the rewind to overwrite them.");

    /// <summary>Tells that a rewind finished.</summary>
    /// <param name="written">The number of files written or deleted.</param>
    /// <param name="skipped">The number of files left alone because they were refused or cannot be restored.</param>
    /// <returns>The sentence.</returns>
    public static string Completed(int written, int skipped)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"Restored {written} {(written == 1 ? "file" : "files")}.");
        return skipped == 0 ? text : string.Create(CultureInfo.InvariantCulture, $"{text} {skipped} could not be restored and {(skipped == 1 ? "was" : "were")} left alone.");
    }

    /// <summary>Tells that a file changed between the plan and the write.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The sentence.</returns>
    public static string ChangedWhileRewinding(string path) =>
        string.Create(CultureInfo.InvariantCulture, $"{path} changed while the rewind was running, so nothing was kept. Try again.");

    /// <summary>Tells that a write failed and the earlier writes were undone.</summary>
    /// <param name="undone">The number of files written before the failure, which were put back.</param>
    /// <param name="reason">The failure.</param>
    /// <returns>The sentence.</returns>
    public static string WriteFailed(int undone, string reason) =>
        string.Create(CultureInfo.InvariantCulture, $"The rewind failed ({reason.TrimEnd('.')}). {undone} {(undone == 1 ? "file written before it was" : "files written before it were")} put back.");
}
