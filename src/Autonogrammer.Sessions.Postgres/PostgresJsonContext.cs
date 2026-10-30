using System.Text.Json.Serialization;
using Autonogrammer.Contracts.Sessions;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// Source-generated JSON metadata for the values this store keeps in jsonb columns that the shared
/// <c>AutonogrammerJsonContext</c> does not already cover: the session message list and the granted-tool list.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Message[]))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class PostgresJsonContext : JsonSerializerContext
{
}
