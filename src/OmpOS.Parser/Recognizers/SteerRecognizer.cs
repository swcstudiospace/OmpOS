using OmpOS.Contracts.Intents;

namespace OmpOS.Parser.Recognizers;

/// <summary>
/// Recognizes non-empty text submitted while a prompt is running on the session as mid-run steering.
/// </summary>
public sealed class SteerRecognizer : IPromptRecognizer
{
    /// <inheritdoc />
    public int Order => 50;

    /// <inheritdoc />
    public RecognizerResult? TryRecognize(PromptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.Context.IsSessionRunning || input.NormalizedText.Length == 0)
        {
            return null;
        }

        return new RecognizerResult
        {
            Kind = IntentKind.Steer,
            Confidence = 0.9,
            Arguments = new Dictionary<string, string>(StringComparer.Ordinal),
            Resources = [],
            RecognizerName = nameof(SteerRecognizer),
        };
    }
}
