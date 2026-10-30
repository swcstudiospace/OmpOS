namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Where an automation run executes.
/// </summary>
public enum AutomationTargetKind
{
    /// <summary>A session on the desktop that registered a local target. The API host has no local target.</summary>
    Local,

    /// <summary>A cloud agent started through the cloud agent control plane.</summary>
    Cloud,

    /// <summary>A desktop session, conversation or code, started through the session starter seam. The API host has no session target.</summary>
    Session,
}
