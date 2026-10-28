using Autonogrammer.Contracts.Intents;

namespace Autonogrammer.Parser;

/// <summary>
/// The claim a recognizer makes over one prompt.
/// </summary>
public sealed record RecognizerResult
{
    /// <summary>Gets the intent kind the recognizer assigns.</summary>
    public required IntentKind Kind { get; init; }

    /// <summary>Gets the recognizer confidence between 0 and 1.</summary>
    public required double Confidence { get; init; }

    /// <summary>Gets the arguments extracted from the prompt.</summary>
    public required IReadOnlyDictionary<string, string> Arguments { get; init; }

    /// <summary>Gets the resources (mentions, file references) referenced by the prompt.</summary>
    public required IReadOnlyList<string> Resources { get; init; }

    /// <summary>Gets the name of the recognizer that produced this result.</summary>
    public required string RecognizerName { get; init; }
}
