namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>The secret the tenant hash is keyed with. Bound from <c>DataPlane:TenantHashPepper</c> by the composition root.</summary>
public sealed class TenantHasherOptions
{
    /// <summary>Gets or sets the pepper. It is a secret and is never logged.</summary>
    public string Pepper { get; set; } = string.Empty;
}
