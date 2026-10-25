namespace OmpOS.ArchitectureTests;

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
        ["OmpOS.Contracts"] = 0,
        ["OmpOS.Parser"] = 1,
        ["OmpOS.Harness.Abstractions"] = 1,
        ["OmpOS.Sandbox.Abstractions"] = 1,
        ["OmpOS.Coordination"] = 1,
        ["OmpOS.Harness.Acp"] = 2,
        ["OmpOS.Harness.Rpc"] = 2,
        ["OmpOS.Harness.InMemory"] = 2,
        ["OmpOS.Harness.Worktrees"] = 2,
        ["OmpOS.Harness.Terminal"] = 2,
        ["OmpOS.Harness.Workspace"] = 2,
        ["OmpOS.Harness.Mcp"] = 2,
        ["OmpOS.Harness.Completion"] = 2,
        ["OmpOS.Harness.Plugins"] = 2,
        ["OmpOS.Harness.Registry"] = 2,
        ["OmpOS.Harness.OmpLauncher"] = 2,
        ["OmpOS.Harness.Automations"] = 3,
        ["OmpOS.Harness.Providers"] = 2,
        ["OmpOS.Harness.Secrets"] = 2,
        ["OmpOS.Harness.Herdr"] = 2,
        ["OmpOS.Harness.Agent"] = 2,
        ["OmpOS.Coordination.Dragonfly"] = 2,
        ["OmpOS.EngineHost"] = 3,
        ["OmpOS.Sessions"] = 2,
        ["OmpOS.Sessions.Postgres"] = 2,
        ["OmpOS.SessionRuns"] = 2,
        ["OmpOS.GenUi"] = 2,
        ["OmpOS.Sandbox"] = 2,
        ["OmpOS.Workflows"] = 2,
        ["OmpOS.UI"] = 2,
        ["OmpOS.Presentation"] = 2,
        ["OmpOS.Api"] = 3,
        ["OmpOS.Web"] = 4,
        ["OmpOS.App"] = 4,
        ["OmpOS.Native"] = 4,
        ["OmpOS.Native.Views"] = 3,
        ["OmpOS.Android"] = 4,
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
        ("OmpOS.Sessions", "OmpOS.GenUi"),
        ("OmpOS.UI", "OmpOS.GenUi"),
        ("OmpOS.UI", "OmpOS.Presentation"),
        ("OmpOS.Presentation", "OmpOS.GenUi"),
        ("OmpOS.Sessions.Postgres", "OmpOS.Sessions"),
        ("OmpOS.SessionRuns", "OmpOS.Sessions"),
        ("OmpOS.SessionRuns", "OmpOS.Workflows"),
        // intentional: the catch-all parser feeds the harness via abstraction types (Phase 2 D-01)
        ("OmpOS.Parser", "OmpOS.Harness.Abstractions"),
        ("OmpOS.Parser", "OmpOS.Sandbox.Abstractions"),
    };
}
