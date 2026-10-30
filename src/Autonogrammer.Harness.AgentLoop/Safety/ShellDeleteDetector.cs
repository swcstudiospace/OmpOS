namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>
/// Recognises shell commands that delete files. It reads the command line with <see cref="ShellCommandAnalysis"/> and does not change those helpers.
/// A deletion built inside a script file is not recognised: the detector never opens or executes the script.
/// </summary>
public static class ShellDeleteDetector
{
    /// <summary>The named gap: a deletion built in a script is not recognised.</summary>
    public const string ScriptDeletionGap = "script-file-deletion";

    private static readonly HashSet<string> DeleteCommands = new(StringComparer.Ordinal)
    {
        "rm", "rmdir", "unlink", "shred",
    };

    /// <summary>
    /// Returns whether the command line is a recognised deletion: <c>rm</c>, <c>rmdir</c>, <c>unlink</c>, <c>shred</c>, <c>find -delete</c>, <c>git clean</c>, including wrapper prefixes.
    /// A deletion built in a script is not recognised.
    /// </summary>
    /// <param name="commandLine">The command line.</param>
    /// <returns>True when a recognised delete command is on the line.</returns>
    public static bool IsDeleting(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return false;
        }

        foreach (var words in ShellCommandAnalysis.CandidatesForDeny(commandLine))
        {
            if (IsDeletingWords(words))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDeletingWords(IReadOnlyList<string> words)
    {
        if (words.Count == 0)
        {
            return false;
        }

        var name = ShellCommandAnalysis.BaseName(words[0]);
        if (DeleteCommands.Contains(name))
        {
            return true;
        }

        if (string.Equals(name, "find", StringComparison.Ordinal))
        {
            for (var index = 1; index < words.Count; index++)
            {
                if (string.Equals(words[index], "-delete", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        if (string.Equals(name, "git", StringComparison.Ordinal))
        {
            for (var index = 1; index < words.Count; index++)
            {
                if (string.Equals(words[index], "clean", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
