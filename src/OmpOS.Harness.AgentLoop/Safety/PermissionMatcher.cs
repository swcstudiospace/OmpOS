namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>Matching of one permission token's argument against one part of a request.</summary>
internal static class PermissionMatcher
{
    /// <summary>Returns whether a <c>Read</c> or <c>Write</c> token's glob matches a path. A pattern with no wildcard also covers everything below it.</summary>
    /// <param name="pattern">The token's argument.</param>
    /// <param name="paths">The path context.</param>
    /// <param name="lexical">The path made absolute without resolving links.</param>
    /// <param name="real">The path with every link resolved, or null when it cannot be resolved.</param>
    /// <param name="forDeny">True for a deny rule: it matches either form of the path. An allow rule matches the real path only, so a link that leaves the folder is not allowed.</param>
    /// <returns>True when the pattern matches.</returns>
    public static bool MatchesPath(string pattern, SafetyPaths paths, string lexical, string? real, bool forDeny)
    {
        var resolved = SafetyPatterns.Resolve(paths, pattern);
        if (resolved.Real is not null && real is not null && Test(resolved.Real, real, paths.IgnoreCase))
        {
            return true;
        }

        return forDeny && Test(resolved.Lexical, lexical, paths.IgnoreCase);
    }

    /// <summary>
    /// Returns whether a <c>Shell</c> token or terminal allowlist entry matches a command. <c>git</c> matches any <c>git</c> command, <c>git status</c> matches
    /// commands that start with those two words, and <c>npm:install*</c> matches <c>npm</c> with arguments that match <c>install*</c>. Matching is case-sensitive.
    /// </summary>
    /// <param name="pattern">The entry.</param>
    /// <param name="words">The words of one simple command.</param>
    /// <param name="forDeny">True to also match the command's file name without its folder, so <c>rm</c> matches <c>/bin/rm</c>.</param>
    /// <returns>True when the entry matches.</returns>
    public static bool MatchesShell(string pattern, IReadOnlyList<string> words, bool forDeny)
    {
        if (words.Count == 0)
        {
            return false;
        }

        var colon = pattern.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && pattern.AsSpan(0, colon).IndexOfAny(' ', '\t') < 0)
        {
            if (!NameMatches(pattern[..colon], words[0], forDeny))
            {
                return false;
            }

            return GlobMatcher.Wildcard(pattern[(colon + 1)..], string.Join(' ', words.Skip(1)), ignoreCase: false);
        }

        var parts = pattern.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || words.Count < parts.Length || !NameMatches(parts[0], words[0], forDeny))
        {
            return false;
        }

        for (var index = 1; index < parts.Length; index++)
        {
            if (!GlobMatcher.Wildcard(parts[index], words[index], ignoreCase: false))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the host an address names, lower case, or null when none can be read. A bare host is accepted.</summary>
    /// <param name="address">The address or host.</param>
    /// <returns>The host, or null.</returns>
    public static string? HostOf(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var text = address.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Host.Length > 0 ? uri.IdnHost : null;
    }

    /// <summary>Returns whether a <c>WebFetch</c> token's domain pattern matches a host. <c>*</c> matches every host and <c>*.example.com</c> matches its subdomains; a plain domain matches itself only.</summary>
    /// <param name="pattern">The token's argument.</param>
    /// <param name="host">The host, lower case.</param>
    /// <returns>True when the pattern matches.</returns>
    public static bool MatchesHost(string pattern, string host) => GlobMatcher.Wildcard(pattern.Trim(), host, ignoreCase: true);

    /// <summary>Returns whether an <c>Mcp</c> token's <c>server:tool</c> matches a call. Both parts ignore case and accept <c>*</c> anywhere.</summary>
    /// <param name="pattern">The token's argument.</param>
    /// <param name="server">The server's name.</param>
    /// <param name="tool">The tool's name.</param>
    /// <returns>True when the pattern matches.</returns>
    public static bool MatchesMcp(string pattern, string server, string tool)
    {
        var colon = pattern.IndexOf(':', StringComparison.Ordinal);
        return colon > 0
            && GlobMatcher.Wildcard(pattern[..colon].Trim(), server, ignoreCase: true)
            && GlobMatcher.Wildcard(pattern[(colon + 1)..].Trim(), tool, ignoreCase: true);
    }

    private static bool Test(string pattern, string path, bool ignoreCase) =>
        GlobMatcher.MatchPath(pattern, path, ignoreCase)
        || (GlobMatcher.IsLiteral(pattern) && GlobMatcher.MatchPath(pattern.TrimEnd('/') + "/**", path, ignoreCase));

    private static bool NameMatches(string pattern, string word, bool allowBaseName) =>
        GlobMatcher.Wildcard(pattern, word, ignoreCase: false)
        || (allowBaseName && GlobMatcher.Wildcard(pattern, ShellCommandAnalysis.BaseName(word), ignoreCase: false));
}
