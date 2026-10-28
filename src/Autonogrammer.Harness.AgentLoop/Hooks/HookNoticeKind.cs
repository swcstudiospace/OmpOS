namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>What a <see cref="HookNotice"/> tells the thread.</summary>
public enum HookNoticeKind
{
    /// <summary>A hook denied an action: <c>denied by hook &lt;name&gt;: &lt;user_message&gt;</c>, attached to the tool call when there is one.</summary>
    Denied,

    /// <summary>A hook that guards an action has no consent, so the action was blocked. The host can ask the user to review it.</summary>
    ConsentRequired,

    /// <summary>A hook added context (<c>additional_context</c>) to the conversation. The thread shows it as a collapsible row.</summary>
    Context,

    /// <summary>A <c>stop</c> hook continued the conversation: <c>Hook continued the conversation (n of loop_limit)</c>.</summary>
    FollowUp,
}
