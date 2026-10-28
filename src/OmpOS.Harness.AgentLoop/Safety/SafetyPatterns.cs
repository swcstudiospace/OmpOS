namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>Turns a path pattern from a token or a read allowlist into absolute patterns that can be matched against paths.</summary>
internal static class SafetyPatterns
{
    /// <summary>A pattern in its two absolute forms.</summary>
    /// <param name="Lexical">The pattern as written, made absolute: <c>~</c> expanded, relative patterns anchored at the workspace root.</param>
    /// <param name="Real">The same pattern with every symlink in its literal prefix resolved, or null when the prefix cannot be resolved.</param>
    internal sealed record Resolved(string Lexical, string? Real);

    /// <summary>Resolves a pattern. A relative pattern is scoped to the workspace root; a leading <c>./</c> is dropped.</summary>
    /// <param name="paths">The path context.</param>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The absolute forms.</returns>
    public static Resolved Resolve(SafetyPaths paths, string pattern)
    {
        var expanded = SafetyPaths.Portable(paths.ExpandHome(pattern.Trim()));
        while (expanded.StartsWith("./", StringComparison.Ordinal))
        {
            expanded = expanded[2..];
        }

        var anchored = Path.GetPathRoot(expanded) is { Length: > 0 } ? expanded : paths.WorkspaceRoot.TrimEnd('/') + "/" + expanded;
        var root = Path.GetPathRoot(anchored) ?? "/";
        var rest = anchored[root.Length..];
        var segments = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var literalCount = 0;
        while (literalCount < segments.Length && GlobMatcher.IsLiteral(segments[literalCount]))
        {
            literalCount++;
        }

        var prefix = Join(root, string.Join('/', segments.Take(literalCount)));
        var remainder = string.Join('/', segments.Skip(literalCount));
        var real = paths.Real(prefix, null);
        return new Resolved(Join(paths.Lexical(prefix, null), remainder), real is null ? null : Join(real, remainder));
    }

    private static string Join(string folder, string rest) =>
        rest.Length == 0 ? folder : folder.TrimEnd('/') + "/" + rest;
}
