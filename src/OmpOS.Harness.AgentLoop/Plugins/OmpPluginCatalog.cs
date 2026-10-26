namespace OmpOS.Harness.AgentLoop.Plugins;

/// <summary>
/// The plugins the agent loop composes, in composition order: the planner first, then the orchestrator, the substrate and
/// the workflow engine. Three live in pinned git submodules; the workflow plugin is tracked by OmpOS itself.
/// </summary>
public static class OmpPluginCatalog
{
    /// <summary>The identifier of the prompt planner.</summary>
    public const string UltrathinkId = "ultrathink";

    /// <summary>The identifier of the subagent orchestrator.</summary>
    public const string SwarmId = "swarm";

    /// <summary>The identifier of the session substrate.</summary>
    public const string SubstrateId = "substrate";

    /// <summary>The identifier of the workflow engine.</summary>
    public const string GsdId = "gsd";

    /// <summary>The built-in plugins in composition order.</summary>
    public static IReadOnlyList<OmpPluginDescriptor> BuiltIn { get; } =
    [
        new OmpPluginDescriptor
        {
            Id = UltrathinkId,
            DisplayName = "Ultrathink",
            Role = OmpPluginRole.PromptPlanner,
            RelativeDirectory = "claude-ultrathink",
            Required = true,
            SubmoduleName = "claude-ultrathink",
            OptionalFixCommand = SubmoduleFix("claude-ultrathink"),
            ExpectedCommands =
            [
                "ultrathink-quick",
                "ultrathink-skip",
                "ultrathink-off",
                "ultrathink-on",
                "ultrathink-track",
                "ultrathink-status",
                "ultrathink-ui",
                "ultrathink-swarm",
            ],
        },
        new OmpPluginDescriptor
        {
            Id = SwarmId,
            DisplayName = "Agent Swarm",
            Role = OmpPluginRole.SubagentOrchestrator,
            RelativeDirectory = "agent-swarm/omp",
            Required = true,
            SubmoduleName = "agent-swarm",
            OptionalFixCommand = SubmoduleFix("agent-swarm"),
            ExpectedCommands = ["swarm"],
            ExpectedTools =
            [
                "swarm_status",
                "swarm_assignment",
                "swarm_plan",
                "swarm_ingest",
                "swarm_transition",
                "swarm_gate",
                "swarm_catalog",
                "swarm_tool_call",
            ],
        },
        new OmpPluginDescriptor
        {
            Id = SubstrateId,
            DisplayName = "Agent Substrate",
            Role = OmpPluginRole.SessionSubstrate,
            RelativeDirectory = "agent-substrate/packages/projector/omp",
            EntryRelativePaths = ["substrate-status.ts", "substrate-skills.ts"],
            Required = false,
            SubmoduleName = "agent-substrate",
            OptionalFixCommand = SubmoduleFix("agent-substrate"),
            ExpectedCommands = ["substrate"],
        },
        new OmpPluginDescriptor
        {
            Id = GsdId,
            DisplayName = "GSD",
            Role = OmpPluginRole.WorkflowEngine,
            RelativeDirectory = "gsd-core-omp",
            Required = false,
            OptionalFixCommand = "git restore plugins/gsd-core-omp",
            ExpectedCommands = ["gsd-core"],
        },
    ];

    private static string SubmoduleFix(string submodule) => "git submodule update --init --recursive plugins/" + submodule;
}
