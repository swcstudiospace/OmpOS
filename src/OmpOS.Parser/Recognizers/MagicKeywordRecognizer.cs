using System.Text.RegularExpressions;
using OmpOS.Contracts.Intents;

namespace OmpOS.Parser.Recognizers;

/// <summary>
/// Recognizes omp magic keywords as standalone, case-sensitive, lowercase whitespace-delimited
/// words in prose. Fenced code, inline code spans and HTML tags never match.
/// </summary>
public sealed partial class MagicKeywordRecognizer : IPromptRecognizer
{
    private static readonly string[] Keywords = ["ultrathink", "orchestrate", "workflowz", "jevify"];

    /// <inheritdoc />
    public int Order => 40;

    /// <inheritdoc />
    public RecognizerResult? TryRecognize(PromptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        foreach (var line in input.ProseLines)
        {
            foreach (Match token in Token().Matches(line))
            {
                if (Array.IndexOf(Keywords, token.Value) >= 0)
                {
                    return new RecognizerResult
                    {
                        Kind = IntentKind.Magic,
                        Confidence = 0.9,
                        Arguments = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["keyword"] = token.Value,
                        },
                        Resources = [],
                        RecognizerName = nameof(MagicKeywordRecognizer),
                    };
                }
            }
        }

        return null;
    }

    [GeneratedRegex("\\S+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Token();
}
