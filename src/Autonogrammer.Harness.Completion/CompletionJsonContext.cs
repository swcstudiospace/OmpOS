using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Completion;

/// <summary>Source-generated JSON metadata for the completion wire shapes and the configuration file.</summary>
[JsonSerializable(typeof(CompletionRequestBody))]
[JsonSerializable(typeof(CompletionResponseBody))]
[JsonSerializable(typeof(InlineEditRequestBody))]
[JsonSerializable(typeof(InlineEditResponseBody))]
[JsonSerializable(typeof(CompletionFileBody))]
internal sealed partial class CompletionJsonContext : JsonSerializerContext
{
}
