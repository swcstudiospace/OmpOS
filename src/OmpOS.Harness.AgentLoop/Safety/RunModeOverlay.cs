using System.Globalization;
using System.Text;

namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>
/// Turns the <c>Shell</c> deny rules into omp's own <c>bash.patterns</c>, which omp enforces in every approval mode, <c>yolo</c> included (a <c>deny</c> pattern is absolute).
/// This is the only enforcement of a deny rule in <see cref="RunMode.RunEverything"/>. omp matches a pattern against the whole command line, so these patterns catch a command
/// that starts with the denied one and not a denied command deep inside a pipeline or a wrapper; OmpOS's own policy reads those when omp asks. <c>bash.patterns</c> is an approval policy, not containment.
/// </summary>
public static class RunModeOverlay
{
    /// <summary>Builds the deny patterns for the <c>Shell</c> deny rules.</summary>
    /// <param name="permissions">The permissions in force.</param>
    /// <returns>The patterns, in rule order, without duplicates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="permissions"/> is null.</exception>
    public static IReadOnlyList<OmpBashPattern> BashDenyPatterns(EffectivePermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var patterns = new List<OmpBashPattern>();
        foreach (var rule in permissions.DenyRules.Where(static rule => rule.Token.Kind == PermissionTokenKind.Shell))
        {
            foreach (var match in Matches(rule.Token.Argument))
            {
                var pattern = new OmpBashPattern(match, "deny");
                if (!patterns.Contains(pattern))
                {
                    patterns.Add(pattern);
                }
            }
        }

        return patterns;
    }

    /// <summary>Renders patterns as the <c>bash</c> block of an omp config overlay.</summary>
    /// <param name="patterns">The patterns.</param>
    /// <returns>The YAML block, or null when there is no pattern.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="patterns"/> is null.</exception>
    public static string? RenderYaml(IReadOnlyList<OmpBashPattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder("bash:\n  patterns:\n");
        foreach (var pattern in patterns)
        {
            builder.Append("    - match: ");
            AppendQuoted(builder, pattern.Match);
            builder.Append("\n      approval: ").Append(pattern.Approval).Append('\n');
        }

        return builder.ToString();
    }

    private static IEnumerable<string> Matches(string argument)
    {
        var colon = argument.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && argument.AsSpan(0, colon).IndexOfAny(' ', '\t') < 0)
        {
            var command = argument[..colon];
            var arguments = argument[(colon + 1)..].Trim();
            if (arguments.Length == 0 || string.Equals(arguments, "*", StringComparison.Ordinal))
            {
                yield return command;
                yield return command + " *";
            }
            else
            {
                yield return command + " " + arguments;
            }

            yield break;
        }

        var words = string.Join(' ', argument.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
        if (words.Length > 0)
        {
            yield return words;
            yield return words + " *";
        }
    }

    private static void AppendQuoted(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case < ' ':
                    builder.Append(CultureInfo.InvariantCulture, $"\\x{(int)character:x2}");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        builder.Append('"');
    }
}
