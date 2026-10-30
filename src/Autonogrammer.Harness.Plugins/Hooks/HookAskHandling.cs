namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// What an event does with the permission value <c>ask</c>.
/// </summary>
public enum HookAskHandling
{
    /// <summary>The event does not use a permission, so the field is ignored.</summary>
    Ignored,

    /// <summary>The event's schema does not accept <c>ask</c>, so it is an invalid response and denies.</summary>
    Invalid,

    /// <summary>The schema accepts <c>ask</c> but it is not enforced, so it counts as allow (Cursor does the same for <c>preToolUse</c>).</summary>
    AsAllow,

    /// <summary>The event treats <c>ask</c> as deny (Cursor does this for <c>subagentStart</c>).</summary>
    AsDeny,

    /// <summary>The host asks the user before the action runs.</summary>
    Enforced,
}
