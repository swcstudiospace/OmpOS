using System.Text.Json;

namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>Reads the embedded <c>chromium-manifest.json</c> rows. A missing or empty document has no rows.</summary>
public sealed class ChromiumManifest
{
    private readonly IReadOnlyList<ChromiumManifestRow> _rows;

    /// <summary>Initializes a manifest from an already parsed row list.</summary>
    /// <param name="rows">The rows. Null is treated as none.</param>
    public ChromiumManifest(IReadOnlyList<ChromiumManifestRow>? rows)
    {
        _rows = rows ?? [];
    }

    /// <summary>Gets the rows. The shipped document has zero.</summary>
    public IReadOnlyList<ChromiumManifestRow> Rows => _rows;

    /// <summary>Loads the embedded manifest. Reflection-based JSON is not used.</summary>
    /// <returns>The manifest.</returns>
    public static ChromiumManifest LoadEmbedded()
    {
        var assembly = typeof(ChromiumManifest).Assembly;
        var name = assembly.GetManifestResourceNames().SingleOrDefault(static candidate => candidate.EndsWith("chromium-manifest.json", StringComparison.Ordinal));
        if (name is null)
        {
            return new ChromiumManifest([]);
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return new ChromiumManifest([]);
        }

        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return new ChromiumManifest([]);
        }

        var parsed = new List<ChromiumManifestRow>();
        foreach (var row in rows.EnumerateArray())
        {
            parsed.Add(new ChromiumManifestRow(
                Required(row, "version"),
                Required(row, "platform"),
                Required(row, "url"),
                Required(row, "sha256"),
                row.GetProperty("sizeBytes").GetInt64()));
        }

        return new ChromiumManifest(parsed);
    }

    /// <summary>Returns the row for <paramref name="platform"/>, or null when the platform is not pinned.</summary>
    /// <param name="platform">The platform id.</param>
    /// <returns>The row, or null.</returns>
    public ChromiumManifestRow? RowFor(string platform)
    {
        ArgumentException.ThrowIfNullOrEmpty(platform);
        foreach (var row in _rows)
        {
            if (string.Equals(row.Platform, platform, StringComparison.Ordinal))
            {
                return row;
            }
        }

        return null;
    }

    private static string Required(JsonElement row, string name)
    {
        return row.GetProperty(name).GetString() ?? throw new InvalidDataException("A manifest row is missing " + name + ".");
    }
}
