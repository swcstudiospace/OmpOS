using System.Text.RegularExpressions;
using OmpOS.Contracts.Intents;

namespace OmpOS.Parser.Recognizers;

/// <summary>
/// Recognizes <c>@mention</c> tokens, <c>in path/to/file</c> phrases and backticked file paths
/// as resources. Fenced code is ignored; inline code is only scanned for backticked paths.
/// </summary>
public sealed partial class MentionRecognizer : IPromptRecognizer
{
    /// <inheritdoc />
    public int Order => 20;

    /// <inheritdoc />
    public RecognizerResult? TryRecognize(PromptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var found = new List<(int Line, int Column, string Value)>();
        for (var line = 0; line < input.ProseLines.Count; line++)
        {
            var text = input.ProseLines[line];
            foreach (Match mention in MentionToken().Matches(text))
            {
                found.Add((line, mention.Index, mention.Value.TrimEnd('.')));
            }

            foreach (Match path in InPhrasePath().Matches(text))
            {
                var group = path.Groups["path"];
                found.Add((line, group.Index, group.Value.TrimEnd('.')));
            }
        }

        for (var line = 0; line < input.Lines.Count; line++)
        {
            if (input.IsInsideCodeFence(line))
            {
                continue;
            }

            foreach (Match span in BacktickSpan().Matches(input.Lines[line]))
            {
                var path = span.Groups["path"].Value;
                if (FilePath().IsMatch(path))
                {
                    found.Add((line, span.Index, path));
                }
            }
        }

        var resources = found
            .OrderBy(static entry => entry.Line)
            .ThenBy(static entry => entry.Column)
            .Select(static entry => entry.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (resources.Length == 0)
        {
            return null;
        }

        return new RecognizerResult
        {
            Kind = IntentKind.Mention,
            Confidence = 0.8,
            Arguments = new Dictionary<string, string>(StringComparer.Ordinal),
            Resources = resources,
            RecognizerName = nameof(MentionRecognizer),
        };
    }

    [GeneratedRegex("(?<!\\S)@\\w[\\w.-]*", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex MentionToken();

    [GeneratedRegex("(?<![\\w-])[Ii][Nn][ \\t]+(?<path>(?:[\\w.-]+/)+[\\w.-]+|[\\w-]+\\.[A-Za-z]\\w*)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex InPhrasePath();

    [GeneratedRegex("`(?<path>[^`\\n]+)`", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex BacktickSpan();

    [GeneratedRegex("\\A(?:(?:[\\w.-]+/)+[\\w.-]+|[\\w-]+\\.[A-Za-z]\\w*)\\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex FilePath();
}
