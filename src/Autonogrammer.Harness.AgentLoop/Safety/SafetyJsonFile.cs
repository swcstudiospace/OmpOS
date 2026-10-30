using System.Text.Json;

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Shared reading of the permission and sandbox files: JSON with comments and trailing commas, a size limit, and diagnostics instead of exceptions.</summary>
internal static class SafetyJsonFile
{
    /// <summary>The largest file Autonogrammer reads, in bytes.</summary>
    public const int MaxBytes = 256 * 1024;

    /// <summary>The most entries Autonogrammer reads from one list.</summary>
    public const int MaxEntries = 500;

    /// <summary>The longest entry Autonogrammer reads, in characters.</summary>
    public const int MaxEntryLength = 512;

    private static readonly JsonDocumentOptions LenientOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 16,
    };

    private static byte[]? ReadBytes(string path, List<SafetyDiagnostic> diagnostics)
    {
        try
        {
            if (new FileInfo(path).Length > MaxBytes)
            {
                diagnostics.Add(new SafetyDiagnostic(path, $"It is larger than {MaxBytes} bytes. It is ignored.", SafetyDiagnosticSeverity.Error));
                return null;
            }

            return File.ReadAllBytes(path);
        }
        catch (IOException exception)
        {
            diagnostics.Add(new SafetyDiagnostic(path, $"It cannot be read: {exception.Message} It is ignored.", SafetyDiagnosticSeverity.Error));
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnostics.Add(new SafetyDiagnostic(path, $"It cannot be read: {exception.Message} It is ignored.", SafetyDiagnosticSeverity.Error));
            return null;
        }
    }

    /// <summary>Opens a file as a JSON object. A missing file is not a problem. Every other failure becomes an error diagnostic and returns null.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="diagnostics">Receives the error, if any.</param>
    /// <param name="exists">True when something is at <paramref name="path"/>.</param>
    /// <returns>The document, which the caller disposes, or null.</returns>
    public static JsonDocument? Open(string path, List<SafetyDiagnostic> diagnostics, out bool exists)
    {
        exists = false;
        if (Directory.Exists(path))
        {
            exists = true;
            diagnostics.Add(new SafetyDiagnostic(path, "It is a folder, not a file. It is ignored.", SafetyDiagnosticSeverity.Error));
            return null;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        exists = true;
        if (ReadBytes(path, diagnostics) is not { } bytes)
        {
            return null;
        }

        var content = bytes.AsMemory();
        if (content.Span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            content = content[3..];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content, LenientOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new SafetyDiagnostic(path, $"It is not valid JSON: {exception.Message} It is ignored.", SafetyDiagnosticSeverity.Error));
            return null;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            diagnostics.Add(new SafetyDiagnostic(path, "It must hold one JSON object. It is ignored.", SafetyDiagnosticSeverity.Error));
            return null;
        }

        return document;
    }

    /// <summary>Reads a list of strings. A missing key returns null; a key of another type is reported and returns null; entries that are not strings are dropped quietly, as Cursor does.</summary>
    /// <param name="parent">The object that holds the key.</param>
    /// <param name="name">The key.</param>
    /// <param name="file">The file, for diagnostics.</param>
    /// <param name="diagnostics">Receives the problems.</param>
    /// <returns>The entries, or null when the key is missing or unusable.</returns>
    public static List<string>? ReadStrings(JsonElement parent, string name, string file, List<SafetyDiagnostic> diagnostics)
    {
        if (!parent.TryGetProperty(name, out var element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new SafetyDiagnostic(file, $"'{name}' must be a list of strings. It is ignored.", SafetyDiagnosticSeverity.Warning));
            return null;
        }

        var entries = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = item.GetString()?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if (text.Length > MaxEntryLength)
            {
                diagnostics.Add(new SafetyDiagnostic(file, $"An entry of '{name}' is longer than {MaxEntryLength} characters. It is ignored.", SafetyDiagnosticSeverity.Warning));
                continue;
            }

            if (entries.Count >= MaxEntries)
            {
                diagnostics.Add(new SafetyDiagnostic(file, $"'{name}' has more than {MaxEntries} entries. The rest are ignored.", SafetyDiagnosticSeverity.Warning));
                break;
            }

            entries.Add(text);
        }

        return entries;
    }
}
