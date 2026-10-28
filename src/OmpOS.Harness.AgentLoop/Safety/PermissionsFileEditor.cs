using System.Text.Json.Nodes;

namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>Edits the allowlists of an OmpOS <c>permissions.json</c>. Other keys, and the entries of other files, are left alone. Only files OmpOS owns can be edited.</summary>
public static class PermissionsFileEditor
{
    /// <summary>The key of the terminal allowlist.</summary>
    public const string TerminalAllowlistKey = "terminalAllowlist";

    /// <summary>The key of the MCP allowlist.</summary>
    public const string McpAllowlistKey = "mcpAllowlist";

    /// <summary>Replaces one allowlist. An empty list is written as <c>[]</c> when the key exists and leaves the file alone when it does not.</summary>
    /// <param name="path">The absolute path of an OmpOS <c>permissions.json</c> (inside a <c>.ompos</c> folder).</param>
    /// <param name="key"><see cref="TerminalAllowlistKey"/> or <see cref="McpAllowlistKey"/>.</param>
    /// <param name="entries">The entries.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static SafetyEditResult SetAllowlist(string path, string key, IReadOnlyList<string> entries)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entries);
        if (!string.Equals(key, TerminalAllowlistKey, StringComparison.Ordinal) && !string.Equals(key, McpAllowlistKey, StringComparison.Ordinal))
        {
            return SafetyEditResult.Failure($"'{key}' is not an allowlist OmpOS edits.");
        }

        return OmposJsonFile.Update(path, root =>
        {
            if (entries.Count == 0 && !root.ContainsKey(key))
            {
                return;
            }

            var array = new JsonArray();
            foreach (var entry in entries)
            {
                array.Add((JsonNode?)JsonValue.Create(entry));
            }

            root[key] = array;
        });
    }
}
