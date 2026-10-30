namespace Autonogrammer.Sandbox;

/// <summary>
/// Result of prerequisite checks.
/// </summary>
public sealed record PrerequisiteCheckResult
{
    /// <summary>Gets whether all prerequisites are met.</summary>
    public required bool AllMet { get; init; }

    /// <summary>Gets the list of failed prerequisites.</summary>
    public required IReadOnlyList<string> FailedPrerequisites { get; init; }

    /// <summary>Gets the podman version found (if any).</summary>
    public string? PodmanVersion { get; init; }

    /// <summary>Gets whether cgroups v2 is enabled.</summary>
    public bool CgroupsV2 { get; init; }

    /// <summary>Gets whether user socket is accessible.</summary>
    public bool UserSocketAccessible { get; init; }

    /// <summary>Creates a success result.</summary>
    public static PrerequisiteCheckResult Success(string podmanVersion) => new()
    {
        AllMet = true,
        FailedPrerequisites = [],
        PodmanVersion = podmanVersion,
        CgroupsV2 = true,
        UserSocketAccessible = true,
    };

    /// <summary>Creates a failure result.</summary>
    public static PrerequisiteCheckResult Failure(IReadOnlyList<string> failed, string? podmanVersion = null, bool cgroupsV2 = false, bool userSocketAccessible = false) => new()
    {
        AllMet = false,
        FailedPrerequisites = failed,
        PodmanVersion = podmanVersion,
        CgroupsV2 = cgroupsV2,
        UserSocketAccessible = userSocketAccessible,
    };
}
