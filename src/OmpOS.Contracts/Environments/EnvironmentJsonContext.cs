using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Environments;

/// <summary>
/// Source-generated JSON metadata for the environment profile shapes and the preflight report. Enums are written as strings; an
/// unknown member in stored data is a deserialization failure the caller reports as an unreadable profile, never a crash.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(EnvironmentProfile))]
[JsonSerializable(typeof(EnvironmentProfile[]))]
[JsonSerializable(typeof(SshProfile))]
[JsonSerializable(typeof(ContainerProfile))]
[JsonSerializable(typeof(WslProfile))]
[JsonSerializable(typeof(ContainerSummary))]
[JsonSerializable(typeof(RemotePreflightReport))]
[JsonSerializable(typeof(RemoteDiagnosis))]
public sealed partial class EnvironmentJsonContext : JsonSerializerContext
{
}
