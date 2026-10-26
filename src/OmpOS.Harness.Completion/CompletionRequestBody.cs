using System.Text.Json.Serialization;

namespace OmpOS.Harness.Completion;

/// <summary>The body posted to the completion provider: the text before and after the caret.</summary>
/// <param name="Prefix">The text before the caret.</param>
/// <param name="Suffix">The text after the caret.</param>
internal sealed record CompletionRequestBody(
    [property: JsonPropertyName("prefix")] string Prefix,
    [property: JsonPropertyName("suffix")] string Suffix);
