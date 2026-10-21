using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmpOS.Harness.Rpc;

/// <summary>
/// Reads a correlation id that omp may echo as a JSON string or as a JSON number, normalizing it to text
/// so that <c>"1"</c> and <c>1</c> correlate with the same command.
/// </summary>
internal sealed class RpcIdConverter : JsonConverter<string?>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            using JsonDocument number = JsonDocument.ParseValue(ref reader);
            return number.RootElement.GetRawText();
        }

        throw new JsonException("An rpc frame id must be a string or a number.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
