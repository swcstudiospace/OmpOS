using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace OmpOS.Harness.Plugins.Tests;

/// <summary>
/// Source-generated JSON metadata for the test manifest builder. Reflection-based serialization is not used anywhere in
/// the solution, so tests serialize their manifests through this context as well. The values inside a
/// <see cref="JsonObject"/> are parsed JSON elements, and their primitive types are declared here.
/// </summary>
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
internal sealed partial class ManifestTestJsonContext : JsonSerializerContext
{
}
