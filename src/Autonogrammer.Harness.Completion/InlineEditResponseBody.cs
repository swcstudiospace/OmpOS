using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Completion;

/// <summary>The body the provider answers an inline edit with. A missing replacement means no proposal; an empty one deletes the selection.</summary>
/// <param name="Replacement">The proposed text for the selected range, or <see langword="null"/> when the provider has none.</param>
internal sealed record InlineEditResponseBody(
    [property: JsonPropertyName("replacement")] string? Replacement);
