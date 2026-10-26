using System.Text.Json.Serialization;
using OmpOS.UI.Security;

namespace OmpOS.UI.Tests.Security;

/// <summary>Source-generated JSON metadata used to prove the holder cannot be serialised.</summary>
[JsonSerializable(typeof(SessionKeyHolder))]
internal sealed partial class SessionKeyJsonContext : JsonSerializerContext
{
}
