using System.Text.Json;
using System.Text.Unicode;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Admits one Dispatch text frame: valid UTF-8, exactly one JSON object, no duplicate property names, <c>v</c> and a known <c>t</c>.
/// Binary-shaped input is refused. Unknown <c>t</c> is an <c>err</c> of <c>unsupported_frame</c> at the type level.
/// </summary>
public static class DispatchFrameGuard
{
    /// <summary>Validates a text frame.</summary>
    /// <param name="text">The UTF-8 bytes of the text frame. Invalid UTF-8 is binary-shaped and refused.</param>
    /// <param name="admission">The admission result.</param>
    /// <returns><see langword="true"/> when the frame is acceptable.</returns>
    public static bool TryAdmit(ReadOnlySpan<byte> text, out DispatchFrameAdmission admission)
    {
        admission = default;
        if (!IsText(text))
        {
            return false;
        }

        if (!TryRead(text, out var type, out var known))
        {
            if (!known && type is not null)
            {
                admission = new DispatchFrameAdmission(false, type, DispatchFrameTypes.UnsupportedFrame);
            }

            return false;
        }

        admission = new DispatchFrameAdmission(true, type, null);
        return true;
    }

    /// <summary>The <c>err</c> frame an unknown <c>t</c> produces. Close 1008 follows after three of these.</summary>
    /// <returns>An error frame whose code is <c>unsupported_frame</c>.</returns>
    public static DispatchErrFrame UnsupportedFrame() => new()
    {
        V = 1,
        T = DispatchFrameTypes.Err,
        Code = DispatchFrameTypes.UnsupportedFrame,
    };

    private static bool IsText(ReadOnlySpan<byte> text) =>
        !text.IsEmpty && text.Length <= DispatchFrameTypes.MaxFrameBytes && Utf8.IsValid(text) && text.IndexOf((byte)0) < 0;

    private static bool TryRead(ReadOnlySpan<byte> text, out string? type, out bool knownType)
    {
        type = null;
        knownType = false;
        var sawVersion = false;
        var reader = new Utf8JsonReader(text, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
        var names = new Stack<HashSet<string>?>();
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject || !Walk(ref reader, names, ref type, ref sawVersion))
            {
                return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        if (names.Count != 0 || reader.BytesConsumed != text.Length || !sawVersion || type is null)
        {
            return false;
        }

        knownType = DispatchFrameTypes.IsKnown(type);
        return knownType;
    }

    private static bool Walk(ref Utf8JsonReader reader, Stack<HashSet<string>?> names, ref string? type, ref bool sawVersion)
    {
        names.Push(new HashSet<string>(StringComparer.Ordinal));
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    if (!TakeProperty(ref reader, names, ref type, ref sawVersion))
                    {
                        return false;
                    }

                    break;
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    Push(names, reader.TokenType);
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    if (names.Count == 0)
                    {
                        return false;
                    }

                    names.Pop();
                    break;
                default:
                    break;
            }
        }

        return true;
    }

    private static bool TakeProperty(ref Utf8JsonReader reader, Stack<HashSet<string>?> names, ref string? type, ref bool sawVersion)
    {
        if (names.Peek() is not { } set)
        {
            return false;
        }

        var name = reader.GetString();
        if (name is null || !set.Add(name))
        {
            return false;
        }

        if (names.Count != 1)
        {
            return true;
        }

        if (string.Equals(name, "t", StringComparison.Ordinal))
        {
            return reader.Read() && reader.TokenType == JsonTokenType.String && AssignType(reader.GetString(), ref type);
        }

        if (string.Equals(name, "v", StringComparison.Ordinal))
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var version) || version != 1)
            {
                return false;
            }

            sawVersion = true;
            return true;
        }

        var look = reader;
        if (!look.Read())
        {
            return false;
        }

        if (look.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            if (!reader.Read())
            {
                return false;
            }

            Push(names, reader.TokenType);
        }

        return true;
    }

    private static bool AssignType(string? value, ref string? type)
    {
        type = value;
        return value is not null;
    }

    private static void Push(Stack<HashSet<string>?> names, JsonTokenType token)
    {
        names.Push(token == JsonTokenType.StartObject ? new HashSet<string>(StringComparer.Ordinal) : null);
    }
}
