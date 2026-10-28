namespace Autonogrammer.Harness.Abstractions;

/// <summary>The outcome of a permission request. <see cref="Deny"/> is the default.</summary>
public enum PermissionDecision
{
    /// <summary>The tool call is refused. The default outcome of every request that receives no grant.</summary>
    Deny = 0,

    /// <summary>The tool call is granted.</summary>
    Allow,
}
