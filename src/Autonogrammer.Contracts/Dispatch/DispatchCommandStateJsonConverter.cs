using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Reads and writes <see cref="DispatchCommandState"/> through <see cref="DispatchCommandStateWire"/>. A name outside the closed set is a <see cref="JsonException"/>.</summary>
public sealed class DispatchCommandStateJsonConverter : JsonConverter<DispatchCommandState>
{
    /// <inheritdoc />
    public override DispatchCommandState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && DispatchCommandStateWire.TryParse(reader.GetString(), out var state))
        {
            return state;
        }

        throw new JsonException("A dispatch command state must be one of the ten closed names.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DispatchCommandState value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(DispatchCommandStateWire.ToWire(value));
    }
}
