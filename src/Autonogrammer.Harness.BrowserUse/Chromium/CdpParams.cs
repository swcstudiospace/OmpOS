using System.Text.Json;

namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>
/// Builds CDP parameter objects as JSON elements. The repository disables reflection-based serialization, and PuppeteerSharp's
/// reflection-free context cannot serialize anonymous types, so every CDP call passes its params through this helper.
/// </summary>
internal static class CdpParams
{
    /// <summary>Parses a JSON object literal into a detached element.</summary>
    /// <param name="json">A JSON object literal.</param>
    /// <returns>The detached root element.</returns>
    public static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Encodes a string as a JSON string literal, with quotes and escaping.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The quoted, escaped JSON string.</returns>
    public static string Quoted(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
}
