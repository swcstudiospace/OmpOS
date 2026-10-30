using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Environments;

/// <summary>
/// Reads and writes <see cref="EnvironmentKind"/> by name, and tolerates a kind this build does not know: a newer build may store a kind that an older one cannot
/// run. Such a value is read as an undefined kind (<see cref="UnknownValue"/> for an unknown name, the number itself for an unknown number) instead of failing the whole
/// document, so one new profile never makes every saved profile unreadable. <see cref="EnvironmentProfileValidator"/> rejects an undefined kind, so it never runs anywhere.
/// A defined kind is written as its name; an undefined one is written as its number, which the next read keeps as it is. The original name of an unknown kind is not preserved: a store rewritten by this build keeps the profile (it is still listed as unusable) but not the word the newer build used.
/// </summary>
public sealed class EnvironmentKindJsonConverter : JsonConverter<EnvironmentKind>
{
    /// <summary>The value an unknown kind name is read as. It is not a member of <see cref="EnvironmentKind"/>.</summary>
    public const int UnknownValue = -1;

    /// <inheritdoc />
    public override EnvironmentKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var name = reader.GetString();
            foreach (var kind in Enum.GetValues<EnvironmentKind>())
            {
                if (string.Equals(kind.ToString(), name, StringComparison.Ordinal))
                {
                    return kind;
                }
            }

            return (EnvironmentKind)UnknownValue;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.TryGetInt32(out var number) ? (EnvironmentKind)number : (EnvironmentKind)UnknownValue;
        }

        throw new JsonException("An environment kind must be a string or a number.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, EnvironmentKind value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (Enum.IsDefined(value))
        {
            writer.WriteStringValue(value.ToString());
        }
        else
        {
            writer.WriteNumberValue((int)value);
        }
    }
}
