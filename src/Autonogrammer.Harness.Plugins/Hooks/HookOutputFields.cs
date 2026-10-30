namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The output fields of a hook that an event honours. Any other field a hook returns is ignored for that event.
/// </summary>
[Flags]
public enum HookOutputFields
{
    /// <summary>No output is used.</summary>
    None = 0,

    /// <summary>The <c>permission</c> field: allow, deny or ask.</summary>
    Permission = 1,

    /// <summary>The <c>user_message</c> field, shown to the user.</summary>
    UserMessage = 2,

    /// <summary>The <c>agent_message</c> field, sent to the model.</summary>
    AgentMessage = 4,

    /// <summary>The <c>updated_input</c> field.</summary>
    UpdatedInput = 8,

    /// <summary>The <c>additional_context</c> field.</summary>
    AdditionalContext = 16,

    /// <summary>The <c>followup_message</c> field.</summary>
    FollowupMessage = 32,

    /// <summary>The <c>env</c> field.</summary>
    Env = 64,

    /// <summary>The <c>continue</c> field.</summary>
    Continue = 128,

    /// <summary>The <c>updated_mcp_tool_output</c> field.</summary>
    UpdatedMcpToolOutput = 256,

    /// <summary>The <c>pluginPaths</c> field of <c>workspaceOpen</c>.</summary>
    PluginPaths = 512,
}
