using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Intents;

namespace Autonogrammer.Parser.Recognizers;

/// <summary>
/// Recognizes omp magic keywords as standalone, case-sensitive prose words at punctuation boundaries.
/// Fenced code, inline code spans and HTML/XML sections never match.
/// </summary>
public sealed partial class MagicKeywordRecognizer : IPromptRecognizer
{
    /// <inheritdoc />
    public int Order => 40;

    /// <inheritdoc />
    public RecognizerResult? TryRecognize(PromptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        foreach (var line in input.ProseLines)
        {
            var match = MagicKeyword().Match(line);
            if (match.Success)
            {
                return new RecognizerResult
                {
                    Kind = IntentKind.Magic,
                    Confidence = 0.9,
                    Arguments = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["keyword"] = match.Value,
                    },
                    Resources = [],
                    RecognizerName = nameof(MagicKeywordRecognizer),
                };
            }
        }

        return null;
    }

    [GeneratedRegex("(?<![\\p{L}\\p{N}_./\\\\-])(?<!::)(?:ultrathink|orchestrate|workflowz|jevify)(?![\\p{L}\\p{N}_/\\\\-])(?!\\.[\\p{L}\\p{N}_-])(?!\\()", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex MagicKeyword();
}
