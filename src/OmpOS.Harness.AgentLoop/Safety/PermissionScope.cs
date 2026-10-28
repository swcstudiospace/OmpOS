namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>Where a permission or sandbox file lives.</summary>
public enum PermissionScope
{
    /// <summary>The user's home directory: it applies to every workspace.</summary>
    User = 0,

    /// <summary>The workspace: it applies to that workspace only and can be committed.</summary>
    Project = 1,
}
