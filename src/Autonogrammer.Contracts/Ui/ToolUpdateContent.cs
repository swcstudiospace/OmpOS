using System.Text.Json;

namespace Autonogrammer.Contracts.Ui;

/// <summary>
/// What a tool-call update carries beyond its status: the output text, the file edits and the images. Read from the
/// update's payload JSON by <see cref="Read"/>.
/// </summary>
/// <param name="Output">The output text, or an empty string when the update carries none.</param>
/// <param name="Edits">The file edits, in the order the update listed them.</param>
/// <param name="Images">The images, in the order the update listed them. A refused payload is a placeholder entry.</param>
public sealed record ToolUpdateContent(string Output, IReadOnlyList<ToolFileEdit> Edits, IReadOnlyList<ToolUpdateImage> Images)
{
    /// <summary>The largest image payload, in decoded bytes, that is kept. A payload at or over this size is a placeholder (D-27).</summary>
    public const int MaxImageBytes = 10 * 1024 * 1024;

    /// <summary>An update that carries neither output, edits nor images.</summary>
    public static ToolUpdateContent None { get; } = new(string.Empty, [], []);

    /// <summary>
    /// Reads the output, the file edits and the images from the payload JSON of a tool-call update. The payload follows
    /// the ACP <c>tool_call_update</c> shape: a <c>content</c> array whose text entries carry output, whose <c>diff</c>
    /// entries carry a path with old and new text, and whose <c>image</c> entries carry <c>data</c>, <c>mimeType</c> and
    /// an optional <c>description</c>. An image may also be nested as a <c>content</c> entry whose inner type is
    /// <c>image</c>. An optional <c>rawOutput</c> stands in for text when no text entry exists. A malformed image entry
    /// is ignored. A payload of <see cref="MaxImageBytes"/> or more is a placeholder entry with an empty
    /// <see cref="ToolUpdateImage.DataBase64"/> and the description kept. Anything else, including text that is not
    /// JSON, yields <see cref="None"/>: reading never throws.
    /// </summary>
    /// <param name="payloadJson">The update's payload JSON text.</param>
    /// <returns>The content the update carries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payloadJson"/> is null.</exception>
    public static ToolUpdateContent Read(string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            return document.RootElement.ValueKind == JsonValueKind.Object ? ReadObject(document.RootElement) : None;
        }
        catch (JsonException)
        {
            return None;
        }
    }

    private static ToolUpdateContent ReadObject(JsonElement root)
    {
        var texts = new List<string>();
        var edits = new List<ToolFileEdit>();
        var images = new List<ToolUpdateImage>();
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in content.EnumerateArray())
            {
                ReadEntry(entry, texts, edits, images);
            }
        }

        if (texts.Count == 0 && root.TryGetProperty("rawOutput", out var raw))
        {
            var rawText = raw.ValueKind switch
            {
                JsonValueKind.String => raw.GetString() ?? string.Empty,
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => raw.GetRawText(),
            };
            if (rawText.Length > 0)
            {
                texts.Add(rawText);
            }
        }

        return new ToolUpdateContent(string.Join('\n', texts), edits, images);
    }

    private static void ReadEntry(JsonElement entry, List<string> texts, List<ToolFileEdit> edits, List<ToolUpdateImage> images)
    {
        if (entry.ValueKind != JsonValueKind.Object || StringOf(entry, "type") is not { } type)
        {
            return;
        }

        if (string.Equals(type, "diff", StringComparison.Ordinal))
        {
            if (StringOf(entry, "path") is { Length: > 0 } path)
            {
                edits.Add(new ToolFileEdit(path, StringOf(entry, "oldText"), StringOf(entry, "newText") ?? string.Empty));
            }

            return;
        }

        if (string.Equals(type, "image", StringComparison.Ordinal))
        {
            ReadImage(entry, images);
            return;
        }

        if (!string.Equals(type, "content", StringComparison.Ordinal)
            || !entry.TryGetProperty("content", out var inner)
            || inner.ValueKind != JsonValueKind.Object
            || StringOf(inner, "type") is not { } innerType)
        {
            return;
        }

        if (string.Equals(innerType, "image", StringComparison.Ordinal))
        {
            ReadImage(inner, images);
            return;
        }

        if (string.Equals(innerType, "text", StringComparison.Ordinal) && StringOf(inner, "text") is { Length: > 0 } text)
        {
            texts.Add(text);
        }
    }

    private static void ReadImage(JsonElement image, List<ToolUpdateImage> images)
    {
        if (StringOf(image, "mimeType") is not { Length: > 0 } mime || StringOf(image, "data") is not { Length: > 0 } data)
        {
            return;
        }

        var description = StringOf(image, "description");
        if (RefusesImage(data))
        {
            images.Add(new ToolUpdateImage(mime, string.Empty, description));
            return;
        }

        try
        {
            _ = Convert.FromBase64String(data);
        }
        catch (FormatException)
        {
            return;
        }

        images.Add(new ToolUpdateImage(mime, data, description));
    }

    /// <summary>True when the base64 text would decode to <see cref="MaxImageBytes"/> or more, without allocating the bytes.</summary>
    /// <param name="dataBase64">The base64 text.</param>
    /// <returns>True when the payload must be a placeholder.</returns>
    public static bool RefusesImage(string dataBase64)
    {
        ArgumentNullException.ThrowIfNull(dataBase64);
        return dataBase64.Length > (MaxImageBytes / 3) * 4;
    }

    /// <summary>The decoded length of a kept base64 payload, from its length and padding. Does not allocate.</summary>
    /// <param name="dataBase64">The base64 text.</param>
    /// <returns>The decoded byte length.</returns>
    public static int DecodedLength(string dataBase64)
    {
        ArgumentNullException.ThrowIfNull(dataBase64);
        var padding = dataBase64.EndsWith("==", StringComparison.Ordinal) ? 2 : dataBase64.EndsWith('=') ? 1 : 0;
        return (dataBase64.Length / 4) * 3 - padding;
    }

    private static string? StringOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
