using System.Globalization;

namespace Autonogrammer.Worktrees;

/// <summary>The sentences of the worktree commands.</summary>
internal static class WorktreeCopy
{
    private const int MaxListedFiles = 5;

    /// <summary>Tells that a run changed nothing.</summary>
    /// <param name="runId">The run.</param>
    /// <returns>The sentence.</returns>
    public static string NothingToApply(string runId) => string.Create(CultureInfo.InvariantCulture, $"Run {runId} changed no files, so there is nothing to apply.");

    /// <summary>Tells that a patch was applied.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="files">The number of files.</param>
    /// <returns>The sentence.</returns>
    public static string Applied(string runId, int files) =>
        string.Create(CultureInfo.InvariantCulture, $"Applied the changes of run {runId} to {files} {(files == 1 ? "file" : "files")}. They are in the working tree, not committed.");

    /// <summary>Tells that a patch was merged three ways without conflicts.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="files">The number of files.</param>
    /// <returns>The sentence.</returns>
    public static string AppliedThreeWay(string runId, int files) =>
        string.Create(CultureInfo.InvariantCulture, $"Merged the changes of run {runId} into {files} {(files == 1 ? "file" : "files")} without conflicts. The merged changes are staged, not committed.");

    /// <summary>Tells that a patch does not fit and nothing was written.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="conflicts">The files that do not fit.</param>
    /// <returns>The sentence.</returns>
    public static string DoesNotFit(string runId, IReadOnlyList<string> conflicts) =>
        string.Create(CultureInfo.InvariantCulture, $"The changes of run {runId} do not fit this checkout{Listed(conflicts)}. Nothing was changed. Review the patch, or merge them three ways.");

    /// <summary>Tells that conflict markers were left in files.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="conflicts">The conflicting files.</param>
    /// <returns>The sentence.</returns>
    public static string ConflictMarkers(string runId, IReadOnlyList<string> conflicts) =>
        string.Create(CultureInfo.InvariantCulture, $"Merged run {runId} with conflicts{Listed(conflicts)}. Resolve the conflict markers in those files.");

    /// <summary>Tells that git failed.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="standardError">What git wrote.</param>
    /// <returns>The sentence.</returns>
    public static string Failed(string runId, string standardError)
    {
        var detail = standardError.Split('\n').Select(static line => line.Trim()).FirstOrDefault(static line => line.Length > 0) ?? "git gave no reason";
        return string.Create(CultureInfo.InvariantCulture, $"Applying run {runId} failed: {detail.TrimEnd('.')}. Nothing was changed.");
    }

    private static string Listed(IReadOnlyList<string> conflicts)
    {
        if (conflicts.Count == 0)
        {
            return string.Empty;
        }

        var shown = string.Join(", ", conflicts.Take(MaxListedFiles));
        return conflicts.Count > MaxListedFiles
            ? string.Create(CultureInfo.InvariantCulture, $" ({shown} and {conflicts.Count - MaxListedFiles} more)")
            : " (" + shown + ")";
    }
}
