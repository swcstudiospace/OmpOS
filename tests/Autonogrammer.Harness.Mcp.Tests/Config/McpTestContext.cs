using Autonogrammer.Harness.Mcp.Config;

namespace Autonogrammer.Harness.Mcp.Tests.Config;

/// <summary>The fixed roots, home and environment the tests read configuration with.</summary>
internal static class McpTestContext
{
    /// <summary>The workspace root.</summary>
    public static readonly string Root = Path.Combine("/work", "app");

    /// <summary>The user's home directory.</summary>
    public static readonly string Home = Path.Combine("/home", "ada");

    /// <summary>Builds a context over one root and the fixed home.</summary>
    /// <param name="environment">The environment variables, as name and value pairs.</param>
    /// <returns>The context.</returns>
    public static McpLocatorContext Create(params (string Name, string? Value)[] environment) =>
        Create([Root], environment);

    /// <summary>Builds a context over several roots and the fixed home.</summary>
    /// <param name="roots">The workspace roots.</param>
    /// <param name="environment">The environment variables, as name and value pairs.</param>
    /// <returns>The context.</returns>
    public static McpLocatorContext Create(string[] roots, params (string Name, string? Value)[] environment)
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in environment)
        {
            variables[name] = value;
        }

        return new McpLocatorContext(roots, Home, variables);
    }

    /// <summary>Returns a path of a project file under the root.</summary>
    /// <param name="relative">The relative path with <c>/</c> separators.</param>
    /// <returns>The absolute path.</returns>
    public static string Project(string relative) => Path.Combine(Root, relative);

    /// <summary>Returns a path of a user file under the home.</summary>
    /// <param name="relative">The relative path with <c>/</c> separators.</param>
    /// <returns>The absolute path.</returns>
    public static string User(string relative) => Path.Combine(Home, relative);

    /// <summary>Quotes text as a JSON string literal. Tests build JSON by hand because reflection-based serialization is off.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The quoted literal.</returns>
    public static string Quote(string text) =>
        "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
