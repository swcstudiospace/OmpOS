namespace Autonogrammer.Harness.Isolation;

/// <summary>How far the omp child is confined. Network is never fenced, so every level still reports network unrestricted.</summary>
public enum IsolationLevel
{
    /// <summary>No confinement. Code uses this by default. A failed probe that the user accepted also uses this.</summary>
    None = 0,

    /// <summary>The process is in a job object (kill on close, no breakaway, memory ceiling) and the filesystem is not isolated.</summary>
    ProcessContained = 1,

    /// <summary>Writes are confined to grants, scratch and omp state directories. This is the Linux and macOS success level.</summary>
    FilesystemIsolated = 2,
}
