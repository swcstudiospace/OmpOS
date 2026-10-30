using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Source-generated JSON metadata for the secret file: one object from reference to value.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class FileAutomationSecretStoreJsonContext : JsonSerializerContext
{
}
