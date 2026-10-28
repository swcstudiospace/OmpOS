using System.Text.Json.Serialization;

namespace Autonogrammer.AgentWorker.Hooks;

/// <summary>The source-generated JSON metadata of the consent snapshot claim.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HooksConsentSnapshot))]
internal sealed partial class WorkerHooksJsonContext : JsonSerializerContext;
