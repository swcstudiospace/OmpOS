namespace Autonogrammer.Sandbox.Abstractions;

/// <summary>
/// Mount type.
/// </summary>
public enum MountType
{
    /// <summary>Bind mount from host.</summary>
    Bind,

    /// <summary>Named volume.</summary>
    Volume,

    /// <summary>Tmpfs mount.</summary>
    Tmpfs,
}
