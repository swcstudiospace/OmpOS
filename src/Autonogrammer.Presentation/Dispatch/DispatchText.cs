namespace Autonogrammer.Presentation.Dispatch;

/// <summary>Plain text for task text in a prompt or a row (D-29): capped, and never parsed as markup.</summary>
public static class DispatchText
{
    /// <summary>The longest task text a prompt or row shows.</summary>
    public const int MaxTaskTextLength = 200;

    /// <summary>Caps the text at <see cref="MaxTaskTextLength"/> characters. Nothing else is interpreted.</summary>
    /// <param name="text">The task text.</param>
    /// <returns>The text, capped.</returns>
    public static string Plain(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length <= MaxTaskTextLength ? text : text[..MaxTaskTextLength];
    }
}
