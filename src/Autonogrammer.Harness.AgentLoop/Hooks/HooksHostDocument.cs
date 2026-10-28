namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>The on-disk form of <see cref="HooksHostStore"/>: the global switches, the trusted workspaces and the last approved version of each hook.</summary>
internal sealed class HooksHostDocument
{
    /// <summary>Gets or sets the schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets a value indicating whether hooks run at all. The default is true.</summary>
    public bool RunHooks { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether Claude settings files are discovered. The default is true.</summary>
    public bool IncludeThirdPartyConfigs { get; set; } = true;

    /// <summary>Gets or sets the workspace roots the user trusted to load project hooks from.</summary>
    public List<string> TrustedWorkspaces { get; set; } = [];

    /// <summary>Gets or sets the last approved version of each hook.</summary>
    public List<HookApprovalSnapshot> Approvals { get; set; } = [];
}
