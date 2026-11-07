using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Accounts;

/// <summary>
/// Source-generated JSON metadata for the account shapes. Names are camelCase, null members are omitted unless a member says otherwise, and enums are
/// written as strings.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AuthConfigResponse))]
[JsonSerializable(typeof(AccountResponse))]
[JsonSerializable(typeof(AccountSessionList))]
[JsonSerializable(typeof(AuthPhase))]
[JsonSerializable(typeof(AuthErrorKind))]
public sealed partial class AccountsJsonContext : JsonSerializerContext
{
}
