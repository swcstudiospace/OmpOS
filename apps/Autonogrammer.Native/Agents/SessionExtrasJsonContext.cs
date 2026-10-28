using System.Text.Json.Serialization;

namespace Autonogrammer.Native.Agents;

/// <summary>Source-generated JSON metadata for <see cref="SessionExtrasDocument"/>. Reflection-based serialization is not used.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(SessionExtrasDocument))]
internal sealed partial class SessionExtrasJsonContext : JsonSerializerContext
{
}
