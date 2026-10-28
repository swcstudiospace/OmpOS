using System.Text.Json.Serialization;
using Autonogrammer.UI.Security;

namespace Autonogrammer.UI.Tests.Security;

/// <summary>Source-generated JSON metadata used to prove the holder cannot be serialised.</summary>
[JsonSerializable(typeof(SessionKeyHolder))]
internal sealed partial class SessionKeyJsonContext : JsonSerializerContext
{
}
