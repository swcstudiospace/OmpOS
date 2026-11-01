namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>
/// The public face of the internal known-safe list. Cowork is a different assembly and must not be granted InternalsVisibleTo.
/// </summary>
public static class KnownSafeShell
{
    /// <summary>Returns whether a command line is a short read-only command whose arguments the caller allows.</summary>
    /// <param name="commandLine">The command line.</param>
    /// <param name="argumentAllowed">Tells whether an argument that may name a file is allowed.</param>
    /// <returns>True when the internal known-safe list accepts the line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="argumentAllowed"/> is null.</exception>
    public static bool IsKnownSafe(string commandLine, Func<string, bool> argumentAllowed)
    {
        ArgumentNullException.ThrowIfNull(argumentAllowed);
        return KnownSafeCommands.IsKnownSafe(ShellCommandAnalysis.ReadForAllow(commandLine ?? string.Empty), argumentAllowed);
    }

    /// <summary>Returns whether a shell permission pattern matches the command line.</summary>
    /// <param name="commandLine">The command line.</param>
    /// <param name="pattern">The <c>Shell</c> token argument.</param>
    /// <param name="forDeny">True to use the deny reading, which looks through wrappers.</param>
    /// <returns>True when the pattern matches.</returns>
    public static bool MatchesShellRule(string commandLine, string pattern, bool forDeny)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        if (forDeny)
        {
            foreach (var words in ShellCommandAnalysis.CandidatesForDeny(commandLine ?? string.Empty))
            {
                if (PermissionMatcher.MatchesShell(pattern, words, forDeny: true))
                {
                    return true;
                }
            }

            return false;
        }

        var reading = ShellCommandAnalysis.ReadForAllow(commandLine ?? string.Empty);
        if (reading.Opaque)
        {
            return false;
        }

        foreach (var words in reading.Segments)
        {
            if (PermissionMatcher.MatchesShell(pattern, words, forDeny: false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an <c>Mcp(server:tool)</c> pattern matches a server and tool.</summary>
    /// <param name="pattern">The token argument, <c>server:tool</c>.</param>
    /// <param name="server">The server name.</param>
    /// <param name="tool">The tool name.</param>
    /// <returns>True when the pattern matches.</returns>
    public static bool MatchesMcpRule(string pattern, string server, string tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        return PermissionMatcher.MatchesMcp(pattern, server ?? string.Empty, tool ?? string.Empty);
    }
}
