namespace Autonogrammer.Contracts.DataPlane;

/// <summary>How a component is doing.</summary>
public enum DataPlaneState
{
    /// <summary>The component works.</summary>
    Healthy,

    /// <summary>The component works with a restriction or a probe is recovering; the reason says which.</summary>
    Degraded,

    /// <summary>The component cannot be used right now.</summary>
    Unavailable,

    /// <summary>The component is not part of this deployment.</summary>
    Disabled,
}
