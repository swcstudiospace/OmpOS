namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Single source of truth for the L0-L4 dependency layers (decision D-02).
/// Adding a project is a one-line change here plus a ProjectReference from
/// the ArchitectureTests project so its assembly lands in the test output.
/// </summary>
internal static class LayerMap
{
    /// <summary>
    /// Maps every product assembly name to its layer number.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> Layers = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Autonogrammer.Contracts"] = 0,
        ["Autonogrammer.Parser"] = 1,
        ["Autonogrammer.Harness.Abstractions"] = 1,
        ["Autonogrammer.Sandbox.Abstractions"] = 1,
        ["Autonogrammer.Coordination"] = 1,
        ["Autonogrammer.Harness.Acp"] = 2,
        ["Autonogrammer.Harness.Rpc"] = 2,
        ["Autonogrammer.Harness.InMemory"] = 2,
        ["Autonogrammer.Harness.Worktrees"] = 2,
        ["Autonogrammer.Harness.Terminal"] = 2,
        ["Autonogrammer.Harness.Workspace"] = 2,
        ["Autonogrammer.Harness.Mcp"] = 2,
        ["Autonogrammer.Harness.McpGateway"] = 2,
        ["Autonogrammer.Harness.Completion"] = 2,
        ["Autonogrammer.Harness.Plugins"] = 2,
        ["Autonogrammer.Harness.Registry"] = 2,
        ["Autonogrammer.Harness.OmpLauncher"] = 2,
        ["Autonogrammer.Harness.OmpConfig"] = 2,
        ["Autonogrammer.Harness.Automations"] = 3,
        ["Autonogrammer.Harness.Providers"] = 2,
        ["Autonogrammer.Harness.Secrets"] = 2,
        ["Autonogrammer.Harness.Herdr"] = 2,
        ["Autonogrammer.Harness.Agent"] = 2,
        ["Autonogrammer.Harness.AgentLoop"] = 2,
        ["Autonogrammer.Harness.Remote"] = 2,
        ["Autonogrammer.Coordination.Dragonfly"] = 2,
        ["Autonogrammer.EngineHost"] = 3,
        ["Autonogrammer.Sessions"] = 2,
        ["Autonogrammer.Sessions.Postgres"] = 2,
        ["Autonogrammer.SessionRuns"] = 2,
        ["Autonogrammer.GenUi"] = 2,
        ["Autonogrammer.Sandbox"] = 2,
        ["Autonogrammer.Workflows"] = 2,
        ["Autonogrammer.UI"] = 2,
        ["Autonogrammer.Presentation"] = 2,
        ["Autonogrammer.Api"] = 3,
        ["Autonogrammer.AgentWorker"] = 3,
        ["Autonogrammer.Web"] = 4,
        ["Autonogrammer.Native"] = 4,
        ["Autonogrammer.Native.Views"] = 3,
        ["Autonogrammer.Android"] = 4,
    };

    /// <summary>
    /// Explicit allow-list for same-layer edges (D-02): Sessions may use GenUi,
    /// the UI razor library may use GenUi and Presentation, and Presentation may use GenUi,
    /// the Postgres store and the shared session-run activities may use
    /// Sessions (and the run activities Workflows), and the catch-all parser may use the
    /// harness and sandbox abstraction types. Every other same-layer edge is a violation.
    /// </summary>
    public static readonly IReadOnlySet<(string From, string To)> SameLayerAllowed = new HashSet<(string, string)>
    {
        ("Autonogrammer.Sessions", "Autonogrammer.GenUi"),
        ("Autonogrammer.UI", "Autonogrammer.GenUi"),
        ("Autonogrammer.UI", "Autonogrammer.Presentation"),
        ("Autonogrammer.Presentation", "Autonogrammer.GenUi"),
        ("Autonogrammer.Sessions.Postgres", "Autonogrammer.Sessions"),
        ("Autonogrammer.SessionRuns", "Autonogrammer.Sessions"),
        ("Autonogrammer.SessionRuns", "Autonogrammer.Workflows"),
        // intentional: the catch-all parser feeds the harness via abstraction types (Phase 2 D-01)
        ("Autonogrammer.Parser", "Autonogrammer.Harness.Abstractions"),
        ("Autonogrammer.Parser", "Autonogrammer.Sandbox.Abstractions"),
        // intentional: the MCP gateway serves the tool registry that the MCP library owns
        ("Autonogrammer.Harness.McpGateway", "Autonogrammer.Harness.Mcp"),
        // intentional: the omp launcher starts omp through the terminal library's process host
        ("Autonogrammer.Harness.OmpLauncher", "Autonogrammer.Harness.Terminal"),
        // intentional: the omp configuration client finds the omp executable through the launcher's locator and wraps API keys in the secrets library's SecretString
        ("Autonogrammer.Harness.OmpConfig", "Autonogrammer.Harness.OmpLauncher"),
        ("Autonogrammer.Harness.OmpConfig", "Autonogrammer.Harness.Secrets"),
        // intentional: the OmpConfig client writes the provider catalogue's rows into omp's models.yml
        ("Autonogrammer.Harness.OmpConfig", "Autonogrammer.Harness.Providers"),
        // intentional: the remote transport speaks ACP and materialises the AgentLoop launch plan on the remote host
        ("Autonogrammer.Harness.Remote", "Autonogrammer.Harness.Acp"),
        ("Autonogrammer.Harness.Remote", "Autonogrammer.Harness.AgentLoop"),
        // intentional: the remote file system and convention source read through the workspace library's file abstractions, launch through the terminal library's process host, and report through the presentation layer's models
        ("Autonogrammer.Harness.Remote", "Autonogrammer.Harness.Terminal"),
        ("Autonogrammer.Harness.Remote", "Autonogrammer.Harness.Workspace"),
        ("Autonogrammer.Harness.Remote", "Autonogrammer.Presentation"),
        // intentional: the hook bridge taps the ACP update stream and installs its plugin through the plugin library
        ("Autonogrammer.Harness.AgentLoop", "Autonogrammer.Harness.Acp"),
        ("Autonogrammer.Harness.AgentLoop", "Autonogrammer.Harness.Plugins"),
        // intentional: the MCP catalog maps configured servers to ACP mcpServers entries
        ("Autonogrammer.Harness.Mcp", "Autonogrammer.Harness.Acp"),
        // intentional: the API hosts the automation scheduler, the webhook receiver and the management routes over the automations library
        ("Autonogrammer.Api", "Autonogrammer.Harness.Automations"),
    };
}
