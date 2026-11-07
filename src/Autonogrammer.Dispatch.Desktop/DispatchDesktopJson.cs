using System.Text.Json.Serialization;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>Source-generated JSON for the desktop state files: policy, pairing and the dedupe window. Names are camel case.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(DispatchPolicy))]
[JsonSerializable(typeof(DispatchPairedDevice))]
[JsonSerializable(typeof(DispatchDedupeDocument))]
internal sealed partial class DispatchDesktopJson : JsonSerializerContext
{
}
