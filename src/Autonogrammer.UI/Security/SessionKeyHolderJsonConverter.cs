using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.UI.Security;

/// <summary>Refuses to serialise or read a <see cref="SessionKeyHolder"/>, so a key can never be written into a JSON document, storage entry or log.</summary>
public sealed class SessionKeyHolderJsonConverter : JsonConverter<SessionKeyHolder>
{
    /// <inheritdoc/>
    public override SessionKeyHolder Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("A session key holder is memory only and cannot be deserialised.");

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SessionKeyHolder value, JsonSerializerOptions options) =>
        throw new NotSupportedException("A session key holder is memory only and cannot be serialised.");
}
