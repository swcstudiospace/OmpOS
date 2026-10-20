using System.Text.RegularExpressions;
using OmpOS.Contracts.Intents;

namespace OmpOS.Parser.Recognizers;

/// <summary>
/// Recognizes a leading slash command such as <c>/resume args</c>. Only matches when the
/// command starts the prompt (line 0) and is not inside a code fence.
/// </summary>
public sealed partial class SlashCommandRecognizer : IPromptRecognizer
{
    /// <inheritdoc />
    public int Order => 10;

    /// <inheritdoc />
    public RecognizerResult? TryRecognize(PromptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsInsideCodeFence(0))
        {
            return null;
        }

        var match = CommandPattern().Match(input.NormalizedText);
        if (!match.Success)
        {
            return null;
        }

        return new RecognizerResult
        {
            Kind = IntentKind.SlashCommand,
            Confidence = 0.95,
            Arguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["command"] = match.Groups["command"].Value,
                ["args"] = match.Groups["args"].Value,
            },
            Resources = [],
            RecognizerName = nameof(SlashCommandRecognizer),
        };
    }

    [GeneratedRegex("\\A/(?<command>[a-z][\\w-]*)(?:\\s+(?<args>[\\s\\S]*))?\\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex CommandPattern();
}
