using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>
/// Source-generated JSON metadata for the provider shapes: the catalogue list, a credential write, the default model, custom endpoints and
/// a provider test. It is a feature context of its own and does not grow the core contract context.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ProvidersResponse))]
[JsonSerializable(typeof(ProviderSummary))]
[JsonSerializable(typeof(ProviderRouteInfo))]
[JsonSerializable(typeof(ProviderUrlVariableInfo))]
[JsonSerializable(typeof(ProviderPlanInfo))]
[JsonSerializable(typeof(ProviderPlanTierInfo))]
[JsonSerializable(typeof(CustomProviderInfo))]
[JsonSerializable(typeof(CustomProviderRequest))]
[JsonSerializable(typeof(SetCredentialRequest))]
[JsonSerializable(typeof(CredentialResponse))]
[JsonSerializable(typeof(SetDefaultModelRequest))]
[JsonSerializable(typeof(DefaultModelResponse))]
[JsonSerializable(typeof(ProviderTestResponse))]
[JsonSerializable(typeof(ProviderRegion))]
[JsonSerializable(typeof(ProviderKind))]
[JsonSerializable(typeof(ProviderVerification))]
[JsonSerializable(typeof(ProviderConfiguredState))]
[JsonSerializable(typeof(CustomProviderProtocol))]
[JsonSerializable(typeof(ProviderTestStatus))]
public sealed partial class ProvidersJsonContext : JsonSerializerContext
{
}
