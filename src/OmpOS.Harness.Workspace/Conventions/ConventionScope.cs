namespace OmpOS.Workspace.Conventions;

/// <summary>Where a discovered convention item lives.</summary>
public enum ConventionScope
{
    /// <summary>A directory of an open workspace root (or a nested folder of it).</summary>
    Project = 0,

    /// <summary>The user's home directory, shared by every workspace.</summary>
    User = 1,
}
