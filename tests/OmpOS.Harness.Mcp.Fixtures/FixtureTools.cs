using ModelContextProtocol.Server;

namespace OmpOS.Harness.Mcp.Fixtures;

/// <summary>The two tools the fixture server exposes, registered from delegates.</summary>
internal static class FixtureTools
{
    /// <summary>Builds the fixture's tool set: <c>echo</c> and <c>add</c>.</summary>
    /// <returns>The tools to register.</returns>
    public static IEnumerable<McpServerTool> All() =>
    [
        McpServerTool.Create(
            (Func<string, string>)Echo,
            new McpServerToolCreateOptions { Name = "echo", Description = "Returns the text it was given." }),
        McpServerTool.Create(
            (Func<int, int, int>)Add,
            new McpServerToolCreateOptions { Name = "add", Description = "Adds two integers." }),
    ];

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
}
