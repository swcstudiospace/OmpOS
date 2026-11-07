using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.PluginBundles.Tests;

/// <summary>Bundle MCP servers stay unconfirmed and expand the plugin root token.</summary>
public sealed class BundleMcpSourceTests
{
    /// <summary>Server <c>s</c> in plugin <c>p</c> is <c>p.s</c>, unconfirmed, with the root token expanded.</summary>
    [Fact]
    public void NamespacesServerAndExpandsRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "ag-bundle-" + Guid.NewGuid().ToString("N"));
        var bundle = Path.Combine(root, "p");
        Directory.CreateDirectory(bundle);
        try
        {
            File.WriteAllText(
                Path.Combine(bundle, ".mcp.json"),
                """{"mcpServers":{"s":{"command":"${CLAUDE_PLUGIN_ROOT}/bin","args":["${CLAUDE_PLUGIN_ROOT}/server.js"],"env":{"ROOT":"${CLAUDE_PLUGIN_ROOT}"}}}}""");
            var server = BundleMcpSource.Read(bundle, "p").Single();
            var full = Path.GetFullPath(bundle);
            server.Name.ShouldBe("p.s");
            server.Confirmed.ShouldBeFalse();
            server.Block.ShouldBe(BundleMcpSource.NotConfirmed);
            server.OriginBundle.ShouldBe("p");
            server.Command.ShouldBe(full + "/bin");
            server.Arguments.ShouldBe([full + "/server.js"]);
            server.Environment.Single().Value.ShouldBe(full);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
