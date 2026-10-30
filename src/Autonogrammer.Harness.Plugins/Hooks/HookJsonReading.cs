using System.Text.Json;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Small checks shared by the hook file readers.
/// </summary>
internal static class HookJsonReading
{
    /// <summary>
    /// Walks a JSON tree and records an error for every object that repeats a key. <see cref="JsonDocument"/> keeps
    /// duplicate keys silently, so a file that sets <c>command</c> twice would otherwise run an unexpected one.
    /// </summary>
    /// <param name="element">The root of the tree to check.</param>
    /// <param name="file">The file name for diagnostics.</param>
    /// <param name="path">The JSON path of <paramref name="element"/>.</param>
    /// <param name="diagnostics">Receives one error per repeated key.</param>
    /// <returns>True when no key is repeated.</returns>
    public static bool CheckNoDuplicateKeys(JsonElement element, string file, string path, List<HookDiagnostic> diagnostics)
    {
        var clean = true;
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                var childPath = $"{path}.{property.Name}";
                if (!seen.Add(property.Name))
                {
                    diagnostics.Add(new HookDiagnostic(
                        HookDiagnosticSeverity.Error,
                        file,
                        childPath,
                        $"The key '{property.Name}' appears more than once."));
                    clean = false;
                }

                clean &= CheckNoDuplicateKeys(property.Value, file, childPath, diagnostics);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                clean &= CheckNoDuplicateKeys(item, file, $"{path}[{index}]", diagnostics);
                index++;
            }
        }

        return clean;
    }

    /// <summary>
    /// Parses UTF-8 JSON with the depth limit and without comments or trailing commas.
    /// </summary>
    /// <param name="utf8Json">The file bytes.</param>
    /// <param name="maxDepth">The deepest nesting allowed.</param>
    /// <param name="file">The file name for diagnostics.</param>
    /// <param name="diagnostics">Receives an error when the JSON is malformed.</param>
    /// <returns>The document, or null when it is malformed. The caller disposes it.</returns>
    public static JsonDocument? TryParse(ReadOnlyMemory<byte> utf8Json, int maxDepth, string file, List<HookDiagnostic> diagnostics)
    {
        try
        {
            return JsonDocument.Parse(utf8Json, new JsonDocumentOptions { MaxDepth = maxDepth });
        }
        catch (JsonException ex)
        {
            diagnostics.Add(new HookDiagnostic(
                HookDiagnosticSeverity.Error,
                file,
                "$",
                $"The file is not valid JSON (comments and trailing commas are not allowed, depth limit {maxDepth}): {ex.Message}"));
            return null;
        }
    }
}
