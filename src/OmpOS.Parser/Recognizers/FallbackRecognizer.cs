using OmpOS.Contracts.Intents;

namespace OmpOS.Parser.Recognizers;

/// <summary>
/// The catch-all terminator. It always matches with confidence 0 and kind <see cref="IntentKind.Fallback"/>.
/// <see cref="PromptParser"/> always appends its own instance last, so a registered instance is
/// ignored for ordering purposes and cannot be outrun.
/// </summary>
public sealed class FallbackRecognizer : IPromptRecognizer
{
    /// <summary>Initializes a new fallback recognizer.</summary>
    /// <param name="order">The registration order. <see cref="PromptParser"/> ignores it and always runs the fallback last.</param>
    public FallbackRecognizer(int order = int.MaxValue)
    {
        Order = order;
    }

    /// <summary>Gets the terminal result shared by every fallback match.</summary>
    internal static RecognizerResult Result { get; } = new()
    {
        Kind = IntentKind.Fallback,
        Confidence = 0.0,
        Arguments = new Dictionary<string, string>(StringComparer.Ordinal),
        Resources = [],
        RecognizerName = nameof(FallbackRecognizer),
    };

    /// <inheritdoc />
    public int Order { get; }

    /// <inheritdoc />
    public RecognizerResult? TryRecognize(PromptInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Result;
    }
}
