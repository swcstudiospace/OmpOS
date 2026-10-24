using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmpOS.Harness.Secrets;

/// <summary>
/// Writes a <see cref="SecretString"/> as <see cref="SecretString.RedactionMarker"/> and refuses to read one. Secrets
/// come from a platform secure store, never from JSON, so every read fails whatever token it meets.
/// </summary>
public sealed class SecretStringJsonConverter : JsonConverter<SecretString>
{
    /// <inheritdoc />
    /// <remarks>Null tokens reach <see cref="Read"/> too, so a null token cannot produce a null secret silently.</remarks>
    public override bool HandleNull => true;

    /// <inheritdoc />
    /// <exception cref="JsonException">Always: a secret cannot be read from JSON.</exception>
    public override SecretString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new JsonException("A SecretString cannot be read from JSON; secrets come from the platform secure store.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SecretString value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(SecretString.RedactionMarker);
    }
}
