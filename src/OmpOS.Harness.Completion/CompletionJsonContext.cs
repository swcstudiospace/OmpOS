using System.Text.Json.Serialization;

namespace OmpOS.Harness.Completion;

/// <summary>Source-generated JSON metadata for the completion wire shapes.</summary>
[JsonSerializable(typeof(CompletionRequestBody))]
[JsonSerializable(typeof(CompletionResponseBody))]
internal sealed partial class CompletionJsonContext : JsonSerializerContext
{
}
