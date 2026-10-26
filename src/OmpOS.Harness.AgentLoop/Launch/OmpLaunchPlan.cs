using System.Globalization;
using System.Text;
using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Launch;

/// <summary>
/// How omp is launched for one session: the arguments after <c>omp acp</c>, the environment the child gets on top of the
/// parent's, the MCP servers for <c>session/new</c>, and the stage events for everything the planner left out.
/// </summary>
public sealed record OmpLaunchPlan
{
    /// <summary>Gets the arguments that follow <c>acp</c>, in order. They are passed one by one, never joined into a shell line.</summary>
    public required IReadOnlyList<string> ExtraArguments { get; init; }

    /// <summary>Gets the environment variables set for the child. A key not listed here is inherited from the parent.</summary>
    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    /// <summary>Gets the MCP servers for <c>session/new</c>. Empty when none applies.</summary>
    public required IReadOnlyList<AgentMcpServerSpec> McpServers { get; init; }

    /// <summary>Gets the absolute path of the workspace, which is the working directory of the child.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the absolute path of the settings overlay the caller must write before launching, or null when none is needed.</summary>
    public string? ConfigOverlayPath { get; init; }

    /// <summary>Gets the degradations decided while planning, for example a plugin that is not initialized or a substrate that is unhealthy.</summary>
    public required IReadOnlyList<StageEvent> PlanningEvents { get; init; }

    /// <summary>
    /// Describes the plan without any value: the number of arguments, the environment variable names and the MCP server names.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"OmpLaunchPlan {{ Arguments = {ExtraArguments.Count}, Environment = [");
        builder.AppendJoin(", ", Environment.Keys.Order(StringComparer.Ordinal));
        builder.Append("], McpServers = [");
        builder.AppendJoin(", ", McpServers.Select(server => server.Name));
        builder.Append("] }");
        return builder.ToString();
    }
}
