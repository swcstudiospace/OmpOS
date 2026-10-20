namespace OmpOS.Contracts.Intents;

/// <summary>
/// The single structured outcome of parsing one raw prompt.
/// </summary>
public sealed record IntentEnvelope
{
    /// <summary>Gets the raw input text preserved byte-for-byte.</summary>
    public required string RawText { get; init; }

    /// <summary>Gets the normalized input text.</summary>
    public required string NormalizedText { get; init; }

    /// <summary>Gets the recognized intent kind.</summary>
    public required IntentKind Kind { get; init; }

    /// <summary>Gets the recognizer-extracted arguments.</summary>
    public required IReadOnlyDictionary<string, string> Arguments { get; init; }

    /// <summary>Gets the referenced resources (files, mentions).</summary>
    public required IReadOnlyList<string> Resources { get; init; }

    /// <summary>Gets the recognizer confidence between 0 and 1.</summary>
    public required double Confidence { get; init; }

    /// <summary>Gets the parser diagnostics.</summary>
    public required IReadOnlyList<Diagnostic> Diagnostics { get; init; }
}
