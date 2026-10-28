namespace Autonogrammer.Presentation.Hooks;

/// <summary>What a hook notice in the thread tells.</summary>
public enum HookNoticeType
{
    /// <summary>A hook denied an action. The notice sits under the tool call it denied.</summary>
    Denied = 0,

    /// <summary>A hook that guards an action has no approval, so the action was blocked.</summary>
    ConsentRequired = 1,

    /// <summary>A hook added context to the conversation. The notice is a collapsible row.</summary>
    Context = 2,

    /// <summary>A stop hook continued the conversation.</summary>
    FollowUp = 3,
}
