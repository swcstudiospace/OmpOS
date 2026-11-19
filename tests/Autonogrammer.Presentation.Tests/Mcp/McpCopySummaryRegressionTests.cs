using Autonogrammer.Presentation.Mcp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Mcp;

/// <summary>The list summary states the same approval rule as the consent gate: a project server that starts a process or reads an environment variable.</summary>
public sealed class McpCopySummaryRegressionTests
{
    [Fact]
    public void TheSummaryStatesTheApprovalRuleForProcessAndEnvironmentServers()
    {
        McpCopy.Summary(2, 0).ShouldContain("for a project server that starts a process or reads an environment variable, approved.", Case.Sensitive);
    }
}
