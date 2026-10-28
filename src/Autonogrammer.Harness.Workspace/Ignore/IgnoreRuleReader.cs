using System.Globalization;

namespace Autonogrammer.Workspace.Ignore;

/// <summary>Reads the rules of one ignore file, the global list or the built-in list, and records what it had to skip.</summary>
internal static class IgnoreRuleReader
{
    /// <summary>Reads the rules of one ignore file. A leading byte order mark is dropped; <c>\n</c> and <c>\r\n</c> both end a line.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="source">The path shown in diagnostics and explanations.</param>
    /// <param name="options">The caps and the case rule.</param>
    /// <param name="diagnostics">Receives one entry per skipped line, and one when the rule limit cuts the file.</param>
    /// <returns>The rules in file order.</returns>
    public static List<IgnoreRule> ReadText(string text, string source, IgnoreOptions options, List<IgnoreDiagnostic> diagnostics)
    {
        var content = text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
        return ReadLines(content.Split('\n'), source, options, diagnostics);
    }

    /// <summary>Reads rules from lines that are already split.</summary>
    /// <param name="lines">The lines; a trailing <c>\r</c> is dropped from each.</param>
    /// <param name="source">The path or list name shown in diagnostics and explanations.</param>
    /// <param name="options">The caps and the case rule.</param>
    /// <param name="diagnostics">Receives one entry per skipped line, and one when the rule limit cuts the list.</param>
    /// <returns>The rules in order.</returns>
    public static List<IgnoreRule> ReadLines(IReadOnlyList<string> lines, string source, IgnoreOptions options, List<IgnoreDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var rules = new List<IgnoreRule>();
        for (var index = 0; index < lines.Count; index++)
        {
            var lineNumber = index + 1;
            var rule = IgnoreRule.Parse(lines[index].TrimEnd('\r'), lineNumber, source, options.CaseSensitive, options.MaxPatternLength, out var problem);
            if (rule is null)
            {
                if (problem is not null)
                {
                    diagnostics.Add(new IgnoreDiagnostic(source, lineNumber, problem));
                }

                continue;
            }

            if (rules.Count >= options.MaxRulesPerFile)
            {
                diagnostics.Add(new IgnoreDiagnostic(
                    source,
                    lineNumber,
                    string.Create(CultureInfo.InvariantCulture, $"The file has more than {options.MaxRulesPerFile} rules; this one and the rest were skipped.")));
                break;
            }

            rules.Add(rule);
        }

        return rules;
    }
}
