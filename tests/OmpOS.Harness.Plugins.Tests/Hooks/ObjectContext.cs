using System.Text.Json.Serialization;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Source-generated JSON metadata for the plain dictionaries the tests serialize.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ObjectContext : JsonSerializerContext
{
}
