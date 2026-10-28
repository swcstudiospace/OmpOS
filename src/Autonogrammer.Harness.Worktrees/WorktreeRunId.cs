using System.Diagnostics.CodeAnalysis;

namespace Autonogrammer.Worktrees;

/// <summary>
/// Validates run ids before they are used in a path or a branch name.
/// </summary>
internal static class WorktreeRunId
{
    /// <summary>
    /// The longest accepted run id.
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Returns true when the run id is 1 to 64 ASCII letters, digits or hyphens.
    /// </summary>
    /// <param name="runId">The candidate run id; may be null.</param>
    public static bool IsValid(string? runId)
    {
        if (runId is null || runId.Length is < 1 or > MaxLength)
        {
            return false;
        }

        foreach (var character in runId)
        {
            var allowed = character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Throws <see cref="InvalidRunIdException"/> unless the run id is valid.
    /// </summary>
    /// <param name="runId">The run id to check.</param>
    /// <param name="paramName">The name of the parameter that carried the run id.</param>
    public static void Validate([NotNull] string? runId, string paramName)
    {
        if (runId is null || !IsValid(runId))
        {
            throw new InvalidRunIdException(paramName, runId);
        }
    }
}
