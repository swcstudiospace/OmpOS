using ModelContextProtocol.Server;

namespace Autonogrammer.Harness.Mcp.Fixtures;

/// <summary>The two tools the fixture server exposes, registered from delegates.</summary>
internal static class FixtureTools
{
    /// <summary>The variable that names a parent-environment variable the fixture reports as set or unset. Unset, the tool list stays echo and add.</summary>
    public const string CanaryNameVariable = "AUTONOGRAMMER_MCP_FIXTURE_CANARY_NAME";

    /// <summary>Builds the fixture's tool set: <c>echo</c> and <c>add</c>, plus a canary tool only when <see cref="CanaryNameVariable"/> is set.</summary>
    /// <returns>The tools to register.</returns>
    public static IEnumerable<McpServerTool> All()
    {
        yield return McpServerTool.Create(
            (Func<string, string>)Echo,
            new McpServerToolCreateOptions { Name = "echo", Description = "Returns the text it was given." });
        yield return McpServerTool.Create(
            (Func<int, int, int>)Add,
            new McpServerToolCreateOptions { Name = "add", Description = "Adds two integers." });

        var canaryName = Environment.GetEnvironmentVariable(CanaryNameVariable);
        if (string.IsNullOrEmpty(canaryName))
        {
            yield break;
        }

        var advertised = Environment.GetEnvironmentVariable(canaryName) is null ? "canary_unset" : "canary_set";
        yield return McpServerTool.Create(
            (Func<string>)Canary,
            new McpServerToolCreateOptions { Name = advertised, Description = "Reports whether the named variable is set." });
    }

    /// <summary>Returns the text it was given.</summary>
    /// <param name="text">The text to return.</param>
    /// <returns>The same text.</returns>
    public static string Echo(string text)
    {
        FixtureCallLog.Record("echo");
        return text;
    }

    /// <summary>Adds two integers.</summary>
    /// <param name="a">The first addend.</param>
    /// <param name="b">The second addend.</param>
    /// <returns>The sum.</returns>
    public static int Add(int a, int b)
    {
        FixtureCallLog.Record("add");
        return a + b;
    }

    /// <summary>Placeholder for the canary tool. The advertised name, not the result, is the observation.</summary>
    /// <returns>A fixed word.</returns>
    public static string Canary() => "canary";
}
