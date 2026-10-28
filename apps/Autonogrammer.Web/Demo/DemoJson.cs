using System.Text.Json.Serialization;

namespace Autonogrammer.Web.Demo;

/// <summary>Source-generated JSON metadata for the few shapes only the Demo builds (small string maps and the plain-text health body).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(DemoPromptAccepted))]
internal sealed partial class DemoJson : JsonSerializerContext
{
}
