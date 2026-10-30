namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Finds the script files a hook command names, so their content can be part of the consent identity. The scan is a
/// heuristic on purpose: it splits the command on whitespace, quotes and shell operators, and keeps every token that looks
/// like a path to an existing file inside the scope root. A script the heuristic misses is still covered by the command
/// text; one it finds can no longer be swapped unseen after consent.
/// </summary>
internal static class HookExecutableScanner
{
    private const int MaxFiles = 8;
    private static readonly char[] Separators = [' ', '\t', '\r', '\n', ';', '|', '&', '<', '>', '(', ')', '"', '\'', '`', '='];

    /// <summary>
    /// Lists the candidate files, relative to the scope root, that lie inside it.
    /// </summary>
    /// <param name="command">The shell string, or null.</param>
    /// <param name="args">The argument vector, or null.</param>
    /// <param name="workingDirectory">The absolute directory the hook runs in.</param>
    /// <param name="scopeRoot">The absolute scope root.</param>
    /// <returns>Pairs of absolute path and root-relative path with <c>/</c> separators, at most eight, without repeats.</returns>
    public static IReadOnlyList<(string Absolute, string Relative)> Candidates(
        string? command,
        IReadOnlyList<string>? args,
        string workingDirectory,
        string scopeRoot)
    {
        var tokens = new List<string>();
        if (command is not null)
        {
            tokens.AddRange(command.Split(Separators, StringSplitOptions.RemoveEmptyEntries));
        }

        if (args is not null)
        {
            tokens.AddRange(args);
        }

        var found = new List<(string Absolute, string Relative)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (found.Count >= MaxFiles)
            {
                break;
            }

            if (!LooksLikePath(token))
            {
                continue;
            }

            string absolute;
            try
            {
                absolute = Path.GetFullPath(token, workingDirectory);
            }
            catch (ArgumentException)
            {
                // A token with characters that cannot be in a path is not a file.
                continue;
            }

            var relative = Path.GetRelativePath(scopeRoot, absolute).Replace('\\', '/');
            if (IsOutside(relative) || !seen.Add(relative))
            {
                continue;
            }

            found.Add((absolute, relative));
        }

        return found;
    }

    private static bool LooksLikePath(string token) =>
        token.Length > 0
        && token[0] != '-'
        && (token.Contains('/', StringComparison.Ordinal) || token.Contains('\\', StringComparison.Ordinal) || token.Contains('.', StringComparison.Ordinal));

    private static bool IsOutside(string relative) =>
        Path.IsPathRooted(relative)
        || string.Equals(relative, "..", StringComparison.Ordinal)
        || relative.StartsWith("../", StringComparison.Ordinal);
}
