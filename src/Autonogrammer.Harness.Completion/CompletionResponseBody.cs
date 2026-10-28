using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Completion;

/// <summary>The body the completion provider answers with. A missing or empty suggestion means none.</summary>
/// <param name="Suggestion">The suggested text, or <see langword="null"/> when the provider has none.</param>
internal sealed record CompletionResponseBody(
    [property: JsonPropertyName("suggestion")] string? Suggestion);
