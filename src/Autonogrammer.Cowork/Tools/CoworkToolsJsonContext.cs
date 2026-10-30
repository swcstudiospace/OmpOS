using System.Text.Json.Serialization;

namespace Autonogrammer.Cowork.Tools;

[JsonSerializable(typeof(List<CoworkGrantRecord>))]
[JsonSerializable(typeof(CoworkAuditLine))]
internal sealed partial class CoworkToolsJsonContext : JsonSerializerContext;
