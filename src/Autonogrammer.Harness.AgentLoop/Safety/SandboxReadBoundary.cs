namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Where the agent may read files without asking.</summary>
public enum SandboxReadBoundary
{
    /// <summary>Anywhere the user can read. The default, as in Cursor.</summary>
    System = 0,

    /// <summary>Inside the workspace and the read allowlist. A read anywhere else asks.</summary>
    Workspace = 1,
}
