using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Intents;

namespace Autonogrammer.Parser.Recognizers;

/// <summary>
/// Recognizes a follow-up after a completed prompt: the session has completed a prompt and the text
/// opens with a follow-up marker (<c>also</c>, <c>then</c>, <c>next</c>).
/// </summary>
public sealed partial class FollowUpRecognizer : IPromptRecognizer
{
    /// <inheritdoc />
    public int Order => 60;

    /// <inheritdoc />
    public RecognizerResult? TryRecognize(PromptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.Context.HasCompletedPrompt || !FollowUpMarker().IsMatch(input.NormalizedText))
        {
            return null;
        }

        return new RecognizerResult
        {
            Kind = IntentKind.FollowUp,
            Confidence = 0.6,
            Arguments = new Dictionary<string, string>(StringComparer.Ordinal),
            Resources = [],
            RecognizerName = nameof(FollowUpRecognizer),
        };
    }

    [GeneratedRegex("\\A(?:also|then|next)(?:\\s|\\z)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex FollowUpMarker();
}
