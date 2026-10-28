namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>
/// The short built-in list Auto-review runs without asking: read-only inspections whose every argument is inside the read boundary.
/// It stands in for the part of Cursor's classifier that can be decided without a model, and is deliberately small. A line with a redirect,
/// a substitution, a subshell or a background job is never known-safe.
/// </summary>
internal static class KnownSafeCommands
{
    private static readonly HashSet<string> Simple = new(StringComparer.Ordinal)
    {
        "pwd", "ls", "cat", "head", "tail", "wc", "echo", "which", "uname", "whoami", "grep",
    };

    private static readonly HashSet<string> GitReadOnly = new(StringComparer.Ordinal)
    {
        "status", "diff", "log", "show", "rev-parse", "ls-files", "blame",
    };

    private static readonly string[] RefusedGitOptions = ["--output", "--ext-diff", "--textconv", "--open-files-in-pager", "-O", "--paginate", "-c"];

    /// <summary>Returns whether a reading of a command line is known-safe.</summary>
    /// <param name="reading">The command line, read for allow matching.</param>
    /// <param name="argumentAllowed">Tells whether an argument that may name a file is inside the read boundary and not denied.</param>
    /// <returns>True when every simple command is on the list and every argument is allowed.</returns>
    public static bool IsKnownSafe(ShellCommandAnalysis.Reading reading, Func<string, bool> argumentAllowed)
    {
        if (reading.Opaque || reading.Segments.Count == 0)
        {
            return false;
        }

        foreach (var words in reading.Segments)
        {
            if (!IsSafeSegment(words, argumentAllowed))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeSegment(IReadOnlyList<string> words, Func<string, bool> argumentAllowed)
    {
        var first = 1;
        if (string.Equals(words[0], "git", StringComparison.Ordinal))
        {
            if (words.Count < 2 || !GitReadOnly.Contains(words[1]))
            {
                return false;
            }

            first = 2;
        }
        else if (!Simple.Contains(words[0]))
        {
            return false;
        }

        for (var index = first; index < words.Count; index++)
        {
            var word = words[index];
            if (word.StartsWith('-'))
            {
                if (first == 2 && Array.Exists(RefusedGitOptions, option => word.StartsWith(option, StringComparison.Ordinal)))
                {
                    return false;
                }

                continue;
            }

            if (!argumentAllowed(word))
            {
                return false;
            }
        }

        return true;
    }
}
