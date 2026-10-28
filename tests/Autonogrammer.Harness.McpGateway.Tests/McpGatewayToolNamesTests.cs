using Autonogrammer.Harness.Mcp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.McpGateway.Tests;

/// <summary>The names the gateway offers tools under.</summary>
public sealed class McpGatewayToolNamesTests
{
    [Fact]
    public void ComposeJoinsServerAndToolWithTheSeparator()
    {
        McpGatewayToolNames.Compose(new McpToolId("fixture", "echo")).ShouldBe("fixture__echo");
        McpGatewayToolNames.Separator.ShouldBe("__");
    }
}
