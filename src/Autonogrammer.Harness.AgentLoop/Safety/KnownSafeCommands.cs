namespace Autonogrammer.Harness.AgentLoop.Safety;

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

    private static readonly Dictionary<string, string> ValueShortOptions = new(StringComparer.Ordinal)
    {
        ["grep"] = "efmABCdDX",
        ["head"] = "nc",
        ["tail"] = "ncs",
        ["ls"] = "IwT",
    };

    private const string GitValueShortOptions = "ULSGMCBlxXun";

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

        var shortValueLetters = first == 2 ? GitValueShortOptions : ValueShortOptions.GetValueOrDefault(words[0], string.Empty);
        for (var index = first; index < words.Count; index++)
        {
            var word = words[index];
            if (word.StartsWith('-'))
            {
                if (first == 2 && Array.Exists(RefusedGitOptions, option => word.StartsWith(option, StringComparison.Ordinal)))
                {
                    return false;
                }

                if (!OptionValuesAllowed(word, shortValueLetters, argumentAllowed))
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

    /// <summary>Checks the value carried inside an option word: the text after '=' for a long option, and the text after each value-taking letter of a short cluster.</summary>
    /// <param name="word">The option word, beginning with a dash.</param>
    /// <param name="shortValueLetters">The short option letters of the command that take a value.</param>
    /// <param name="argumentAllowed">Tells whether an argument that may name a file is allowed.</param>
    /// <returns>True when every value carried by the option word is allowed.</returns>
    private static bool OptionValuesAllowed(string word, string shortValueLetters, Func<string, bool> argumentAllowed)
    {
        if (word.StartsWith("--", StringComparison.Ordinal))
        {
            var equals = word.IndexOf('=', StringComparison.Ordinal);
            return equals < 0 || equals == word.Length - 1 || argumentAllowed(word[(equals + 1)..]);
        }

        for (var index = 1; index < word.Length - 1; index++)
        {
            if (shortValueLetters.Contains(word[index], StringComparison.Ordinal) && !argumentAllowed(word[(index + 1)..]))
            {
                return false;
            }
        }

        return true;
    }
}
