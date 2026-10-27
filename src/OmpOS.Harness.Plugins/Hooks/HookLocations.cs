namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Where a hook is allowed to run: on this machine, in a remote or hosted workspace, or both.
/// </summary>
[Flags]
public enum HookLocations
{
    /// <summary>The hook never runs.</summary>
    None = 0,

    /// <summary>The hook runs in local sessions.</summary>
    Local = 1,

    /// <summary>The hook runs in remote and hosted sessions.</summary>
    Remote = 2,

    /// <summary>The hook runs everywhere. This is the default.</summary>
    Both = Local | Remote,
}
