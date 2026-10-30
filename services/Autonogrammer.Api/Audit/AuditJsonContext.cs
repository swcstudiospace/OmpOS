using System.Text.Json.Serialization;

namespace Autonogrammer.Api.Audit;

/// <summary>Source-generated JSON metadata for <see cref="AuditLedgerEvent"/>. Reflection serialisation is off.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AuditLedgerEvent))]
internal sealed partial class AuditJsonContext : JsonSerializerContext
{
}
