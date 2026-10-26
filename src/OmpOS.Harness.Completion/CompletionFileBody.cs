using System.Text.Json.Serialization;

namespace OmpOS.Harness.Completion;

/// <summary>The shape of the completion configuration file. Unknown fields are ignored.</summary>
/// <param name="Endpoint">The model provider endpoint, or <see langword="null"/> when the file configures none.</param>
internal sealed record CompletionFileBody(
    [property: JsonPropertyName("endpoint")] string? Endpoint);
