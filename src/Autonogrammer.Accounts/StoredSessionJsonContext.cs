using System.Text.Json.Serialization;

namespace Autonogrammer.Accounts;

/// <summary>
/// Source-generated JSON metadata for the stored session. It is strict: a member the shape does not know is a failure, so a tampered or newer secret
/// is discarded instead of half read. The shape is <c>{schema, issuer, refreshToken, userId, email}</c>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(StoredSession))]
internal sealed partial class StoredSessionJsonContext : JsonSerializerContext
{
}
