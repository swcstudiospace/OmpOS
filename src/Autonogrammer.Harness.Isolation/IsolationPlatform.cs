namespace Autonogrammer.Harness.Isolation;

/// <summary>The operating system the isolation policy is planned for. Windows has no filesystem wrapper in this phase.</summary>
public enum IsolationPlatform
{
    /// <summary>Linux bubblewrap.</summary>
    Linux = 0,

    /// <summary>macOS Seatbelt.</summary>
    MacOs = 1,

    /// <summary>Windows. The planner reports <c>platform_unsupported</c> until a post-start job-object hook exists.</summary>
    Windows = 2,
}
