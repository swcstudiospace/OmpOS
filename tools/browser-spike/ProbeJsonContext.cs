using System.Text.Json.Serialization;

namespace OmpOS.BrowserSpike;

/// <summary>Source-generated JSON metadata for the probe; no reflection-based serialization.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ProbeReport))]
[JsonSerializable(typeof(string))]
internal sealed partial class ProbeJsonContext : JsonSerializerContext;
