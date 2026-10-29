using System.Text.Json.Serialization;
using Autonogrammer.UI.Components.Auth;

namespace Autonogrammer.Web.Services;

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
[JsonSerializable(typeof(ToolDecisionRequest))]
[JsonSerializable(typeof(ApiProblemBody))]
[JsonSerializable(typeof(AuthConfig))]
internal sealed partial class WebJsonContext : JsonSerializerContext
{
}
