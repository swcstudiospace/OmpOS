namespace Autonogrammer.Presentation.Safety;

/// <summary>Where the agent may read files without asking.</summary>
public enum ReadBoundaryChoice
{
    /// <summary>Anywhere. The default.</summary>
    System = 0,

    /// <summary>Inside the workspace and the read allowlist; anywhere else asks.</summary>
    Workspace = 1,
}
