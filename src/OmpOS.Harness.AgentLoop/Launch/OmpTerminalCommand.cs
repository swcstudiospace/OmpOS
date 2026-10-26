using System.Globalization;
using System.Text;
using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Launch;

/// <summary>
/// How an interactive omp (a terminal session) is launched: the whole argument array after the <c>omp</c> executable, the
/// environment on top of the parent's, and the working directory. It carries no MCP servers: an interactive omp reads its MCP
/// servers from its own configuration and has no <c>session/new</c> to pass them in, so the substrate reaches a terminal only
/// through its plugins.
/// </summary>
public sealed record OmpTerminalCommand
{
    /// <summary>Gets the arguments after the executable, in order. They are passed one by one, never joined into a shell line. They never contain <c>acp</c>.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Gets the environment variables set for the child. A key not listed here is inherited from the parent.</summary>
    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    /// <summary>Gets the absolute path of the workspace, which is the working directory of the child.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the absolute path of the settings overlay the caller must write before launching, or null when none is needed.</summary>
    public string? ConfigOverlayPath { get; init; }

    /// <summary>Gets the degradations decided while planning, for example a plugin that is not initialized.</summary>
    public required IReadOnlyList<StageEvent> PlanningEvents { get; init; }

    /// <summary>
    /// Describes the command without any value: the number of arguments and the environment variable names. The resume
    /// reference and the prompt never appear.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"OmpTerminalCommand {{ Arguments = {Arguments.Count}, Environment = [");
        builder.AppendJoin(", ", Environment.Keys.Order(StringComparer.Ordinal));
        builder.Append("] }");
        return builder.ToString();
    }
}
