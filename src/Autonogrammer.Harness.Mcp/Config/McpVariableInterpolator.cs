using System.Text;

namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>
/// Expands <c>${env:NAME}</c>, <c>${workspaceFolder}</c>, <c>${workspaceFolderBasename}</c>, <c>${userHome}</c>, <c>${pathSeparator}</c> and
/// <c>${/}</c>; in Claude Code files also <c>${NAME}</c> and <c>${NAME:-default}</c>. A reference that cannot be expanded is reported by name and
/// stays in the text: nothing is ever expanded to an empty string silently. <c>${input:id}</c> (VS Code) is not supported and is reported as unresolved.
/// </summary>
internal static class McpVariableInterpolator
{
    /// <summary>Expands the references in one string.</summary>
    /// <param name="text">The configured text.</param>
    /// <param name="context">The values to expand to.</param>
    /// <returns>The expanded text and what could not be expanded.</returns>
    public static McpInterpolationResult Expand(string text, McpInterpolationContext context)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(context);

        if (!text.Contains("${", StringComparison.Ordinal))
        {
            return new McpInterpolationResult(text, [], [], UsedEnvironment: false);
        }

        var builder = new StringBuilder(text.Length);
        var unresolved = new List<string>();
        var unknown = new List<string>();
        var usedEnvironment = false;
        var position = 0;
        while (position < text.Length)
        {
            var open = text.IndexOf("${", position, StringComparison.Ordinal);
            if (open < 0)
            {
                _ = builder.Append(text, position, text.Length - position);
                break;
            }

            var close = text.IndexOf('}', open + 2);
            if (close < 0)
            {
                _ = builder.Append(text, position, text.Length - position);
                break;
            }

            _ = builder.Append(text, position, open - position);
            var name = text.Substring(open + 2, close - open - 2);
            var verbatim = text.Substring(open, close - open + 1);
            var replacement = Resolve(name, context, unresolved, unknown, ref usedEnvironment);
            _ = builder.Append(replacement ?? verbatim);
            position = close + 1;
        }

        return new McpInterpolationResult(builder.ToString(), unresolved, unknown, usedEnvironment);
    }

    private static string? Resolve(
        string name,
        McpInterpolationContext context,
        List<string> unresolved,
        List<string> unknown,
        ref bool usedEnvironment)
    {
        switch (name)
        {
            case "workspaceFolder":
                return Required(context.WorkspaceFolder, name, unresolved);
            case "workspaceFolderBasename":
                return context.WorkspaceFolder is null
                    ? Required(null, name, unresolved)
                    : Basename(context.WorkspaceFolder);
            case "userHome":
                return Required(context.UserHome, name, unresolved);
            case "pathSeparator":
            case "/":
                return new string(Path.DirectorySeparatorChar, 1);
        }

        if (name.StartsWith("env:", StringComparison.Ordinal))
        {
            usedEnvironment = true;
            return Required(Lookup(context, name[4..]), name, unresolved);
        }

        if (name.StartsWith("input:", StringComparison.Ordinal))
        {
            unresolved.Add(name);
            return null;
        }

        if (context.ClaudeStyle && TryResolveClaude(name, context, unresolved, ref usedEnvironment, out var claude))
        {
            return claude;
        }

        unknown.Add(name);
        return null;
    }

    private static bool TryResolveClaude(
        string name,
        McpInterpolationContext context,
        List<string> unresolved,
        ref bool usedEnvironment,
        out string? value)
    {
        value = null;
        var separator = name.IndexOf(":-", StringComparison.Ordinal);
        var variable = separator < 0 ? name : name[..separator];
        if (!IsVariableName(variable))
        {
            return false;
        }

        usedEnvironment = true;
        var found = Lookup(context, variable);
        if (found is not null)
        {
            value = found;
            return true;
        }

        if (separator >= 0)
        {
            value = name[(separator + 2)..];
            return true;
        }

        unresolved.Add(variable);
        return true;
    }

    private static string? Lookup(McpInterpolationContext context, string variable) =>
        context.Environment.TryGetValue(variable, out var found) ? found : null;

    private static string? Required(string? value, string name, List<string> unresolved)
    {
        if (value is not null)
        {
            return value;
        }

        unresolved.Add(name);
        return null;
    }

    private static string Basename(string folder)
    {
        var trimmed = folder.TrimEnd('/', '\\');
        var name = Path.GetFileName(trimmed);
        return name.Length == 0 ? trimmed : name;
    }

    /// <summary>Returns whether a name is a valid environment variable name: a letter or underscore, then letters, digits and underscores.</summary>
    /// <param name="name">The name.</param>
    /// <returns>True when the name is valid.</returns>
    public static bool IsVariableName(string name)
    {
        if (name.Length == 0 || !(char.IsAsciiLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character == '_'))
            {
                return false;
            }
        }

        return true;
    }
}
