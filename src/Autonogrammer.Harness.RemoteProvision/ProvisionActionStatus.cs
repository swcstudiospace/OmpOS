namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>The state of one confirmed install after a provision.</summary>
public enum ProvisionActionStatus
{
    /// <summary>The install's script exited with code 0.</summary>
    Succeeded,

    /// <summary>The install's script failed, or the connection did not carry it through.</summary>
    Failed,

    /// <summary>An earlier install failed, so this one was not started.</summary>
    NotRun,
}
