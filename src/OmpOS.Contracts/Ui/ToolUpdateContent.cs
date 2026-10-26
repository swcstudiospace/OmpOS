using System.Text.Json;

namespace OmpOS.Contracts.Ui;

/// <summary>
/// What a tool-call update carries beyond its status: the output text and the file edits. Read from the update's
/// payload JSON by <see cref="Read"/>.
/// </summary>
/// <param name="Output">The output text, or an empty string when the update carries none.</param>
/// <param name="Edits">The file edits, in the order the update listed them.</param>
public sealed record ToolUpdateContent(string Output, IReadOnlyList<ToolFileEdit> Edits)
{
    /// <summary>An update that carries neither output nor edits.</summary>
    public static ToolUpdateContent None { get; } = new(string.Empty, []);

    /// <summary>
    /// Reads the output and the file edits from the payload JSON of a tool-call update. The payload follows the ACP
    /// <c>tool_call_update</c> shape: a <c>content</c> array whose <c>content</c> entries carry text and whose <c>diff</c>
    /// entries carry a path with old and new text, and an optional <c>rawOutput</c> that stands in for text when no
    /// text entry exists. Anything else, including text that is not JSON, yields <see cref="None"/>: reading never throws.
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
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in content.EnumerateArray())
            {
                ReadEntry(entry, texts, edits);
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

        return new ToolUpdateContent(string.Join('\n', texts), edits);
    }

    private static void ReadEntry(JsonElement entry, List<string> texts, List<ToolFileEdit> edits)
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

        if (string.Equals(type, "content", StringComparison.Ordinal)
            && entry.TryGetProperty("content", out var inner)
            && inner.ValueKind == JsonValueKind.Object
            && string.Equals(StringOf(inner, "type"), "text", StringComparison.Ordinal)
            && StringOf(inner, "text") is { Length: > 0 } text)
        {
            texts.Add(text);
        }
    }

    private static string? StringOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
