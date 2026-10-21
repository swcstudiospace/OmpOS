using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// Source-generated JSON metadata for the podman-compose document.
/// Consumers pass the generated type info instead of relying on reflection.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ComposeDocument))]
public sealed partial class SandboxJsonContext : JsonSerializerContext
{
}
