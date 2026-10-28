using System.Text.Json;

namespace Autonogrammer.BrowserSpike;

/// <summary>The fields the probe script returns for the fixture's button.</summary>
internal sealed record ProbeReport(bool Found, string? Selector, bool Resolves, double X, double Y, double Width, double Height)
{
    /// <summary>Parses the script result, accepting either the object itself or a JSON string literal that wraps it.</summary>
    public static ProbeReport Parse(string raw)
    {
        string payload = raw.TrimStart().StartsWith('"')
            ? JsonSerializer.Deserialize(raw, ProbeJsonContext.Default.String) ?? string.Empty
            : raw;
        return JsonSerializer.Deserialize(payload, ProbeJsonContext.Default.ProbeReport)
            ?? throw new JsonException("probe payload was empty");
    }
}
