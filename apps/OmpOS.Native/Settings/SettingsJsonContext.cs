using System.Text.Json.Serialization;

namespace OmpOS.Native.Settings;

/// <summary>
/// Source-generated JSON metadata for <see cref="SettingsDocument"/>. Reflection-based serialization is not used. Property names are
/// camelCase and null members are not written. Unmapped members are collected by each type's extension data, never dropped.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(SettingsDocument))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
