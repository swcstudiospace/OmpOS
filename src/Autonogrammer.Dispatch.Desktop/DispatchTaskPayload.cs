using System.Text.Json;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// The task fields a dispatched <c>code.prompt</c> or <c>cowork.task</c> payload may carry: the tool classes it names and the task text.
/// The text is shown to the local user only as plain text and only its first 200 characters.
/// </summary>
/// <param name="ToolClasses">Tool classes named by the payload, in order. Empty for a code prompt.</param>
/// <param name="Text">The task text, or an empty string when absent.</param>
public sealed record DispatchTaskPayload(IReadOnlyList<string> ToolClasses, string Text)
{
    /// <summary>The tool class name for Browser Use.</summary>
    public const string BrowserUse = "browser_use";

    /// <summary>The tool class name for Computer Use.</summary>
    public const string ComputerUse = "computer_use";

    /// <summary>The most characters of task text shown in an approval prompt.</summary>
    public const int MaxApprovalTextChars = 200;

    /// <summary>Parses a payload.</summary>
    /// <param name="json">The decoded payload, UTF-8 JSON.</param>
    /// <param name="payload">The parsed payload when the result is true.</param>
    /// <returns>False when the payload is not a JSON object with a string array <c>toolClasses</c> and a string <c>text</c>.</returns>
    public static bool TryParse(ReadOnlyMemory<byte> json, out DispatchTaskPayload payload)
    {
        payload = new DispatchTaskPayload([], string.Empty);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var classes = new List<string>();
            if (root.TryGetProperty("toolClasses", out var list))
            {
                if (list.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (var item in list.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        return false;
                    }

                    classes.Add(item.GetString() ?? string.Empty);
                }
            }

            var text = string.Empty;
            if (root.TryGetProperty("text", out var textElement))
            {
                if (textElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                text = textElement.GetString() ?? string.Empty;
            }

            payload = new DispatchTaskPayload(classes, text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Returns the first <see cref="MaxApprovalTextChars"/> characters of the task text.</summary>
    /// <returns>The shortened text.</returns>
    public string ApprovalText() => Text.Length <= MaxApprovalTextChars ? Text : Text[..MaxApprovalTextChars];
}
