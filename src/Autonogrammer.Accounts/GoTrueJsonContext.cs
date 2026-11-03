using System.Text.Json.Serialization;

namespace Autonogrammer.Accounts;

/// <summary>
/// Source-generated JSON metadata for the wire shapes of the auth service. Names are snake_case and unknown members are skipped, because the service
/// adds fields. Reflection serialisation is off in this repository, so every wire type is listed here.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OtpRequest))]
[JsonSerializable(typeof(VerifyRequest))]
[JsonSerializable(typeof(PkceTokenRequest))]
[JsonSerializable(typeof(RefreshTokenRequest))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(UserResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(Web3TokenRequest))]
[JsonSerializable(typeof(SettingsResponse))]
internal sealed partial class GoTrueJsonContext : JsonSerializerContext
{
}
