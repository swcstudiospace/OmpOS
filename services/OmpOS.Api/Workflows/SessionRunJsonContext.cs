using System.Text.Json.Serialization;
using OmpOS.Workflows;

namespace OmpOS.Api.Workflows;

/// <summary>
/// Source-generated JSON metadata for the session-run workflow argument. Reflection-based serialization is
/// disabled for this assembly, so the Temporal payload converter must use this context.
/// </summary>
/// <remarks>
/// The options must stay identical to the worker's <c>SessionRunPayloadJsonContext</c> (camelCase names,
/// null properties omitted, string enums) so the worker deserializes the same payload.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SessionRunInput))]
internal sealed partial class SessionRunJsonContext : JsonSerializerContext
{
}
