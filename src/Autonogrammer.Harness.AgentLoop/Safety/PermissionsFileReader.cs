using System.Text.Json;

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>
/// Reads one <c>permissions.json</c>. The keys are Cursor's: <c>mcpAllowlist</c>, <c>terminalAllowlist</c> and <c>autoRun</c> with
/// <c>allow_instructions</c> and <c>block_instructions</c>. Autonogrammer adds one more, <c>permissions</c> with <c>allow</c> and <c>deny</c> lists of
/// permission tokens, the shape Cursor's CLI uses in its own configuration, because Cursor's <c>permissions.json</c> has no deny. Unknown keys are ignored.
/// Reading never throws: a file that cannot be used is reported and ignored.
/// </summary>
public static class PermissionsFileReader
{
    /// <summary>Reads one file.</summary>
    /// <param name="location">The file to read.</param>
    /// <returns>What the file says, or an invalid document with diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public static PermissionsFileDocument Read(SafetyFileLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        var diagnostics = new List<SafetyDiagnostic>();
        using var document = SafetyJsonFile.Open(location.Path, diagnostics, out var exists);
        if (document is null)
        {
            return new PermissionsFileDocument(location, exists, IsValid: !exists, null, null, [], [], [], [], diagnostics);
        }

        var root = document.RootElement;
        var terminal = SafetyJsonFile.ReadStrings(root, "terminalAllowlist", location.Path, diagnostics);
        var mcp = ReadMcp(root, location.Path, diagnostics);
        List<string> allowInstructions = [];
        List<string> blockInstructions = [];
        if (root.TryGetProperty("autoRun", out var autoRun))
        {
            if (autoRun.ValueKind == JsonValueKind.Object)
            {
                allowInstructions = SafetyJsonFile.ReadStrings(autoRun, "allow_instructions", location.Path, diagnostics) ?? [];
                blockInstructions = SafetyJsonFile.ReadStrings(autoRun, "block_instructions", location.Path, diagnostics) ?? [];
            }
            else
            {
                diagnostics.Add(new SafetyDiagnostic(location.Path, "'autoRun' must be an object. It is ignored.", SafetyDiagnosticSeverity.Warning));
            }
        }

        List<PermissionToken> allow = [];
        List<PermissionToken> deny = [];
        if (root.TryGetProperty("permissions", out var permissions))
        {
            if (permissions.ValueKind == JsonValueKind.Object)
            {
                allow = ReadTokens(permissions, "allow", location.Path, diagnostics);
                deny = ReadTokens(permissions, "deny", location.Path, diagnostics);
            }
            else
            {
                diagnostics.Add(new SafetyDiagnostic(location.Path, "'permissions' must be an object with 'allow' and 'deny' lists. It is ignored.", SafetyDiagnosticSeverity.Warning));
            }
        }

        return new PermissionsFileDocument(location, Exists: true, IsValid: true, terminal, mcp, allowInstructions, blockInstructions, allow, deny, diagnostics);
    }

    private static List<string>? ReadMcp(JsonElement root, string file, List<SafetyDiagnostic> diagnostics)
    {
        var entries = SafetyJsonFile.ReadStrings(root, "mcpAllowlist", file, diagnostics);
        if (entries is null)
        {
            return null;
        }

        var kept = new List<string>(entries.Count);
        foreach (var entry in entries)
        {
            var colon = entry.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0 || colon == entry.Length - 1)
            {
                diagnostics.Add(new SafetyDiagnostic(file, $"The mcpAllowlist entry '{entry}' has no 'server:tool' form. It is ignored.", SafetyDiagnosticSeverity.Warning));
                continue;
            }

            kept.Add(entry);
        }

        return kept;
    }

    private static List<PermissionToken> ReadTokens(JsonElement parent, string name, string file, List<SafetyDiagnostic> diagnostics)
    {
        var tokens = new List<PermissionToken>();
        foreach (var text in SafetyJsonFile.ReadStrings(parent, name, file, diagnostics) ?? [])
        {
            var result = PermissionTokenParser.Parse(text);
            if (result.Token is { } token)
            {
                tokens.Add(token);
            }
            else
            {
                diagnostics.Add(new SafetyDiagnostic(file, $"The token '{text}' in permissions.{name} is invalid: {result.Error}. It is ignored.", SafetyDiagnosticSeverity.Warning));
            }
        }

        return tokens;
    }
}
