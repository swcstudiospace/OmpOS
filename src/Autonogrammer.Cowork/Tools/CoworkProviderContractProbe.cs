
namespace Autonogrammer.Cowork.Tools;

/// <summary>The facts the contract base passes the provider factory. SessionReleased is set by EndSessionAsync.</summary>
public sealed class CoworkProviderContractProbe
{
    /// <summary>Gets or sets the permission mode under test.</summary>
    public CoworkPermissionMode Mode { get; set; }

    /// <summary>Gets or sets whether the session is tainted.</summary>
    public bool Tainted { get; set; }

    /// <summary>Gets or sets the action under test.</summary>
    public CoworkContractAction Action { get; set; }

    /// <summary>Gets or sets whether EndSessionAsync released the session.</summary>
    public bool SessionReleased { get; set; }
}
