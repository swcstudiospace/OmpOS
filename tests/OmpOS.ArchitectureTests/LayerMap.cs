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
        ["OmpOS.Harness.Acp"] = 2,
        ["OmpOS.Harness.Rpc"] = 2,
        ["OmpOS.Harness.InMemory"] = 2,
        ["OmpOS.EngineHost"] = 3,
        ["OmpOS.Sessions"] = 2,
        ["OmpOS.GenUi"] = 2,
        ["OmpOS.Sandbox"] = 2,
        ["OmpOS.Workflows"] = 2,
        ["OmpOS.UI"] = 2,
        ["OmpOS.Api"] = 3,
        ["OmpOS.Workflows.Worker"] = 3,
        ["OmpOS.Web"] = 4,
        ["OmpOS.App"] = 4,
    };

    /// <summary>
    /// Explicit allow-list for same-layer edges (D-02): Sessions may use GenUi,
    /// and the UI razor library may use GenUi, and the catch-all parser may use the
    /// harness and sandbox abstraction types. Every other same-layer edge is a violation.
    /// </summary>
    public static readonly IReadOnlySet<(string From, string To)> SameLayerAllowed = new HashSet<(string, string)>
    {
        ("OmpOS.Sessions", "OmpOS.GenUi"),
        ("OmpOS.UI", "OmpOS.GenUi"),
        // intentional: the catch-all parser feeds the harness via abstraction types (Phase 2 D-01)
        ("OmpOS.Parser", "OmpOS.Harness.Abstractions"),
        ("OmpOS.Parser", "OmpOS.Sandbox.Abstractions"),
    };
}
