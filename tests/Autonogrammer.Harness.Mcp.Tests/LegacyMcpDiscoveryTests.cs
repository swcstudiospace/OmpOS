using Autonogrammer.Harness.Mcp.Config;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests;

/// <summary>Legacy mcp.json is listed between the current file and Cursor when it exists, and omitted when it does not.</summary>
public sealed class LegacyMcpDiscoveryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("legacy-mcp-").FullName;

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>The four layouts: new only, legacy only, both, and neither. Legacy sits between new and Cursor.</summary>
    [Fact]
    public void ProjectAndUserLegacyFilesSitBetweenNewAndCursor()
    {
        var home = Path.Combine(_root, "home");
        var project = Path.Combine(_root, "project");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(home);
        var neither = Origins(project, home);
        neither.ShouldBe([".autonogrammer/mcp.json", ".cursor/mcp.json", ".mcp.json", ".vscode/mcp.json", "~/.autonogrammer/mcp.json", "~/.cursor/mcp.json", "~/.claude.json"]);

        Write(project, LegacyBrandNames.ConventionDirectory + "/mcp.json");
        Write(home, LegacyBrandNames.ConventionDirectory + "/mcp.json");
        var legacyOnly = Origins(project, home);
        legacyOnly[0].ShouldBe(".autonogrammer/mcp.json");
        legacyOnly[1].ShouldBe(LegacyBrandNames.ConventionDirectory + "/mcp.json");
        legacyOnly[2].ShouldBe(".cursor/mcp.json");
        At(legacyOnly, ".autonogrammer/mcp.json").ShouldBe(0);

        Write(project, ".autonogrammer/mcp.json");
        var both = Origins(project, home);
        At(both, ".autonogrammer/mcp.json").ShouldBeLessThan(At(both, LegacyBrandNames.ConventionDirectory + "/mcp.json"));
    }

    private static List<string> Origins(string project, string home) =>
        [.. McpLocations.Enumerate(new McpLocatorContext([project], home, new Dictionary<string, string?>(StringComparer.Ordinal))).Select(location => location.Origin)];

    private static int At(IReadOnlyList<string> origins, string value) => origins.ToList().FindIndex(item => string.Equals(item, value, StringComparison.Ordinal));

    private static void Write(string root, string relative)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
    }
}
