using System.Text.Json.Serialization;

namespace OmpOS.Web.Services;

/// <summary>
/// Source-generated JSON metadata for the web head's API DTOs.
/// Reflection-free so the trimmer keeps the WASM publish intact.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CreateSessionRequest))]
[JsonSerializable(typeof(SubmitPromptRequest))]
internal sealed partial class WebJsonContext : JsonSerializerContext
{
}
