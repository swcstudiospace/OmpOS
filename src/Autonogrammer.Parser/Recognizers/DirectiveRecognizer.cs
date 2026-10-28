using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Intents;

namespace Autonogrammer.Parser.Recognizers;

/// <summary>
/// Recognizes structured <c>key: value</c> directives at the start of lines outside code fences.
/// At least one directive line is required; later duplicates of a key overwrite earlier ones.
/// </summary>
public sealed partial class DirectiveRecognizer : IPromptRecognizer
{
    /// <inheritdoc />
    public int Order => 30;

    /// <inheritdoc />
    public RecognizerResult? TryRecognize(PromptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var line = 0; line < input.Lines.Count; line++)
        {
            if (input.IsInsideCodeFence(line))
            {
                continue;
            }

            var match = DirectiveLine().Match(input.Lines[line]);
            if (match.Success)
            {
                arguments[match.Groups["key"].Value] = match.Groups["value"].Value;
            }
        }

        if (arguments.Count == 0)
        {
            return null;
        }

        return new RecognizerResult
        {
            Kind = IntentKind.Directive,
            Confidence = 0.8,
            Arguments = arguments,
            Resources = [],
            RecognizerName = nameof(DirectiveRecognizer),
        };
    }

    [GeneratedRegex("\\A(?<key>[A-Za-z][\\w-]*):(?:[ \\t]+(?<value>.*?))?[ \\t]*\\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex DirectiveLine();
}
