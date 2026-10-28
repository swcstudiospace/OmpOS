using System.Globalization;
using System.Text;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Launch;

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

    /// <summary>
    /// Gets the absolute paths of the other roots of a multi-root workspace, in order. Each also appears in <see cref="ExtraArguments"/> as
    /// <c>--add-dir &lt;path&gt;</c>. Empty for a single-root session and when the plan was not made by the planner.
    /// </summary>
    public IReadOnlyList<string> AdditionalDirectories { get; init; } = [];

    /// <summary>Gets the degradations decided while planning, for example a plugin that is not initialized or a substrate that is unhealthy.</summary>
    public required IReadOnlyList<StageEvent> PlanningEvents { get; init; }

    /// <summary>
    /// Gets the identifiers (see <see cref="Plugins.OmpPluginCatalog"/>) of the plugins the plan loads, in composition order.
    /// A plugin that is switched off, missing or unhealthy is not listed. Empty when the plan was not made by the planner.
    /// </summary>
    public IReadOnlyList<string> LoadedPluginIds { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the plan loads the hooks bridge extension (<c>-e plugins/autonogrammer-hooks-omp</c>) and sets its environment. False
    /// when the plan was made without a <see cref="HookBridgeEndpoint"/> or the extension is missing or unhealthy.
    /// </summary>
    public bool HooksBridgeLoaded { get; init; }

    /// <summary>
    /// Derives the launch of an interactive omp (a terminal session) from the plan: the same plugin arguments, environment,
    /// working directory and planning events, never <c>acp</c> and never <c>--plugin-dir</c>, plus an optional resume and
    /// an optional first prompt. The MCP servers of the plan are not part of it: an interactive omp reads its MCP servers
    /// from its own configuration.
    /// </summary>
    /// <param name="options">The resume reference and first prompt, or null for neither.</param>
    /// <returns>The terminal command. It is a new value; the plan is unchanged.</returns>
    /// <exception cref="ArgumentException">
    /// The resume reference is blank-but-not-null, starts with <c>-</c> or contains a NUL character, or the prompt starts with
    /// <c>-</c> or <c>@</c> or contains a NUL character.
    /// </exception>
    public OmpTerminalCommand ForTerminal(OmpTerminalOptions? options = null)
    {
        var arguments = new List<string>(ExtraArguments.Count + 3);
        arguments.AddRange(ExtraArguments);

        if (options?.NativeSessionId is { } resume)
        {
            ValidateResume(resume);
            arguments.Add("--resume");
            arguments.Add(resume);
        }

        if (options?.InitialPrompt is { } prompt && !string.IsNullOrWhiteSpace(prompt))
        {
            ValidatePrompt(prompt);
            arguments.Add(prompt);
        }

        return new OmpTerminalCommand
        {
            Arguments = arguments,
            Environment = Environment,
            WorkingDirectory = WorkingDirectory,
            ConfigOverlayPath = ConfigOverlayPath,
            PlanningEvents = PlanningEvents,
        };
    }

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

    private static void ValidateResume(string resume)
    {
        if (string.IsNullOrWhiteSpace(resume) || resume[0] == '-' || resume.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("The resume reference must be a non-blank value that does not start with '-'.", nameof(resume));
        }
    }

    private static void ValidatePrompt(string prompt)
    {
        if (prompt[0] is '-' or '@' || prompt.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("The initial prompt must not start with '-' or '@' and must not contain a NUL character.", nameof(prompt));
        }
    }
}
