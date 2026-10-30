namespace Autonogrammer.Sandbox;

/// <summary>
/// Status of a Railway deployment. The documented table at
/// https://docs.railway.com/integrations/api/manage-deployments.md lists BUILDING, DEPLOYING, SUCCESS, FAILED, CRASHED,
/// REMOVED, SLEEPING, SKIPPED, WAITING and QUEUED; INITIALIZING, REMOVING and NEEDS_APPROVAL are
/// UNVERIFIED: introspection needed (query __type(name: "DeploymentStatus")).
/// </summary>
public enum RailwayDeploymentStatus
{
    /// <summary>The status string is not recognised, or the service has no deployment.</summary>
    Unknown,

    /// <summary>INITIALIZING (UNVERIFIED: not in the documented status table).</summary>
    Initializing,

    /// <summary>BUILDING.</summary>
    Building,

    /// <summary>DEPLOYING.</summary>
    Deploying,

    /// <summary>SUCCESS.</summary>
    Success,

    /// <summary>FAILED.</summary>
    Failed,

    /// <summary>CRASHED.</summary>
    Crashed,

    /// <summary>REMOVED.</summary>
    Removed,

    /// <summary>REMOVING (UNVERIFIED: not in the documented status table).</summary>
    Removing,

    /// <summary>SLEEPING.</summary>
    Sleeping,

    /// <summary>SKIPPED.</summary>
    Skipped,

    /// <summary>WAITING.</summary>
    Waiting,

    /// <summary>QUEUED.</summary>
    Queued,

    /// <summary>NEEDS_APPROVAL (UNVERIFIED: not in the documented status table).</summary>
    NeedsApproval,
}
