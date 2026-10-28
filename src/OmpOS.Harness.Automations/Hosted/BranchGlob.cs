namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Matches branch names against globs. <c>*</c> matches any run of characters except <c>/</c>, <c>**</c> matches any run including
/// <c>/</c>, <c>?</c> matches one character except <c>/</c>, every other character matches itself. Matching is case sensitive. The
/// match is a dynamic program over the pattern and the text, so no pattern can make it exponential.
/// </summary>
public static class BranchGlob
{
    /// <summary>Returns whether a branch name matches a glob.</summary>
    /// <param name="pattern">The glob.</param>
    /// <param name="branch">The branch name, without a <c>refs/heads/</c> prefix.</param>
    /// <returns>True when the whole name matches.</returns>
    public static bool IsMatch(string pattern, string branch)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(branch);

        // reachable[j] is true when the pattern consumed so far can match the first j characters of the text.
        var reachable = new bool[branch.Length + 1];
        reachable[0] = true;
        var index = 0;
        while (index < pattern.Length)
        {
            var next = new bool[branch.Length + 1];
            var token = pattern[index];
            if (token == '*')
            {
                var crossesSegments = index + 1 < pattern.Length && pattern[index + 1] == '*';
                index += crossesSegments ? 2 : 1;
                var open = false;
                for (var position = 0; position <= branch.Length; position++)
                {
                    open |= reachable[position];
                    next[position] = open;
                    if (open && position < branch.Length && !crossesSegments && branch[position] == '/')
                    {
                        open = false;
                    }
                }
            }
            else
            {
                index++;
                for (var position = 0; position < branch.Length; position++)
                {
                    if (reachable[position] && (token == '?' ? branch[position] != '/' : branch[position] == token))
                    {
                        next[position + 1] = true;
                    }
                }
            }

            reachable = next;
        }

        return reachable[branch.Length];
    }

    /// <summary>Returns whether a branch matches any glob of a list, or the list is empty.</summary>
    /// <param name="patterns">The globs.</param>
    /// <param name="branch">The branch name.</param>
    /// <returns>True when the list is empty or one glob matches.</returns>
    public static bool MatchesAnyOrNoFilter(IReadOnlyList<string> patterns, string branch)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return patterns.Count == 0 || patterns.Any(pattern => IsMatch(pattern, branch));
    }
}
