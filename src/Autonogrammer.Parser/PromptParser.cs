using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Contracts.Intents;
using Autonogrammer.Parser.Recognizers;

namespace Autonogrammer.Parser;

/// <summary>
/// Turns any prompt string into exactly one <see cref="IntentEnvelope"/> by running an ordered
/// recognizer chain. The <see cref="FallbackRecognizer"/> terminator is always appended last and
/// cannot be removed by registration. <see cref="Parse"/> never throws.
/// </summary>
public sealed class PromptParser
{
    /// <summary>The documented maximum prompt length in UTF-16 code units (PARSE-01).</summary>
    public const int MaxPromptLength = 64_000;

    private readonly IReadOnlyList<IPromptRecognizer> _recognizers;

    /// <summary>Initializes a new parser over the given recognizers.</summary>
    /// <param name="recognizers">Registered recognizers; any <see cref="FallbackRecognizer"/> instance is replaced by the hardwired terminator.</param>
    public PromptParser(IEnumerable<IPromptRecognizer> recognizers)
    {
        ArgumentNullException.ThrowIfNull(recognizers);
        _recognizers = recognizers
            .Where(static recognizer => recognizer is not FallbackRecognizer)
            .OrderBy(static recognizer => recognizer.Order)
            .Append(new FallbackRecognizer())
            .ToArray();
    }

    /// <summary>Parses one prompt into exactly one intent envelope.</summary>
    /// <param name="rawText">The prompt text; <see langword="null"/> is treated as empty.</param>
    /// <param name="context">Optional session facts; defaults to an idle session with no completed prompt.</param>
    /// <returns>The envelope. <see cref="IntentEnvelope.RawText"/> is the input unless it exceeded <see cref="MaxPromptLength"/>.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The parser contract is that Parse never throws; any failure becomes a parser_exception diagnostic.")]
    public IntentEnvelope Parse(string? rawText, PromptContext? context = null)
    {
        var raw = rawText ?? string.Empty;
        var diagnostics = new List<Diagnostic>();
        try
        {
            if (raw.Length > MaxPromptLength)
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = "prompt_truncated",
                    Severity = "warning",
                    Message = $"Prompt of {raw.Length} characters exceeds the {MaxPromptLength}-character limit; RawText was truncated.",
                });
                raw = TruncatePrompt(raw);
            }

            var input = PromptInput.Create(raw, context ?? new PromptContext());
            var match = FindMatch(input, diagnostics);
            return new IntentEnvelope
            {
                RawText = raw,
                NormalizedText = input.NormalizedText,
                Kind = match.Kind,
                Arguments = match.Arguments.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
                Resources = match.Resources.ToArray(),
                Confidence = ClampConfidence(match.Confidence),
                Diagnostics = diagnostics.ToArray(),
            };
        }
        catch (Exception ex)
        {
            // Last-resort guard: normalization or masking failed outside any recognizer.
            diagnostics.Add(new Diagnostic
            {
                Code = "parser_exception",
                Severity = "error",
                Message = $"Prompt parsing failed with {ex.GetType().Name}: {ex.Message}",
            });
            return new IntentEnvelope
            {
                RawText = raw,
                NormalizedText = string.Empty,
                Kind = IntentKind.Fallback,
                Arguments = new Dictionary<string, string>(StringComparer.Ordinal),
                Resources = [],
                Confidence = 0.0,
                Diagnostics = diagnostics.ToArray(),
            };
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A recognizer may fail with any exception; it is isolated and reported as a diagnostic so the chain continues.")]
    private RecognizerResult FindMatch(PromptInput input, List<Diagnostic> diagnostics)
    {
        foreach (var recognizer in _recognizers)
        {
            try
            {
                var result = recognizer.TryRecognize(input);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = "recognizer_exception",
                    Severity = "error",
                    Message = $"Recognizer '{recognizer.GetType().Name}' threw {ex.GetType().Name}: {ex.Message}",
                });
            }
        }

        // Reached only if the terminator itself threw, which its implementation cannot do.
        return FallbackRecognizer.Result;
    }

    private static string TruncatePrompt(string raw)
    {
        var length = MaxPromptLength;
        if (char.IsHighSurrogate(raw[length - 1]))
        {
            length--;
        }

        return raw[..length];
    }

    private static double ClampConfidence(double confidence) =>
        double.IsNaN(confidence) ? 0.0 : Math.Clamp(confidence, 0.0, 1.0);
}
