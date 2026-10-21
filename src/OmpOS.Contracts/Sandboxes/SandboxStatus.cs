namespace OmpOS.Contracts.Sandboxes;

/// <summary>
/// Lifecycle states of a sandbox.
/// </summary>
public enum SandboxStatus
{
    /// <summary>Provisioning requested but not started.</summary>
    Requested,

    /// <summary>Provisioning in progress.</summary>
    Provisioning,

    /// <summary>Provisioned and ready to attach.</summary>
    Ready,

    /// <summary>Attached to a session.</summary>
    Attached,

    /// <summary>Attached but degraded.</summary>
    Degraded,

    /// <summary>Teardown in progress.</summary>
    TearingDown,

    /// <summary>Torn down.</summary>
    Destroyed,

    /// <summary>Provisioning or teardown failed.</summary>
    Failed,
}
