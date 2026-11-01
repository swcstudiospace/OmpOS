using System.Text.Json.Nodes;

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Edits the allowlists of an Autonogrammer <c>permissions.json</c>. Other keys, and the entries of other files, are left alone. Only files Autonogrammer owns can be edited.</summary>
public static class PermissionsFileEditor
{
    /// <summary>The key of the terminal allowlist.</summary>
    public const string TerminalAllowlistKey = "terminalAllowlist";

    /// <summary>The key of the MCP allowlist.</summary>
    public const string McpAllowlistKey = "mcpAllowlist";

    /// <summary>The allow list inside the <c>permissions</c> object.</summary>
    public const string AllowTokensList = "allow";

    /// <summary>The deny list inside the <c>permissions</c> object.</summary>
    public const string DenyTokensList = "deny";

    /// <summary>Replaces one allowlist. An empty list is written as <c>[]</c> when the key exists and leaves the file alone when it does not.</summary>
    /// <param name="path">The absolute path of an Autonogrammer <c>permissions.json</c> (inside a <c>.autonogrammer</c> folder).</param>
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
            return SafetyEditResult.Failure($"'{key}' is not an allowlist Autonogrammer edits.");
        }

        return AutonogrammerJsonFile.Update(path, root =>
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

    /// <summary>
    /// Replaces <c>permissions.allow</c> or <c>permissions.deny</c>. Other root keys, and the other list, stay. The path must be an
    /// Autonogrammer <c>permissions.json</c>. The file is owner-readable and owner-writable.
    /// </summary>
    /// <param name="path">The absolute path of an Autonogrammer <c>permissions.json</c>.</param>
    /// <param name="list"><see cref="AllowTokensList"/> or <see cref="DenyTokensList"/>.</param>
    /// <param name="tokens">The tokens to write. An empty list clears that list.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static SafetyEditResult SetPermissionTokens(string path, string list, IReadOnlyList<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(tokens);
        if (!string.Equals(list, AllowTokensList, StringComparison.Ordinal) && !string.Equals(list, DenyTokensList, StringComparison.Ordinal))
        {
            return SafetyEditResult.Failure($"'{list}' is not a permission token list Autonogrammer edits.");
        }

        var result = AutonogrammerJsonFile.Update(path, root =>
        {
            var permissions = root["permissions"] as JsonObject ?? new JsonObject();
            var array = new JsonArray();
            foreach (var token in tokens)
            {
                array.Add((JsonNode?)JsonValue.Create(token));
            }

            permissions[list] = array;
            root["permissions"] = permissions;
        });
        if (result.Succeeded && !OperatingSystem.IsWindows() && File.Exists(path))
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return result;
    }
}
