namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// The lifecycle events a hook can attach to. The manifest names them in camelCase, for example <c>beforeAction</c>.
/// </summary>
public enum HookTrigger
{
    /// <summary>A session starts.</summary>
    SessionStart,

    /// <summary>An action is about to run. A blocking hook can deny it.</summary>
    BeforeAction,

    /// <summary>An action has finished.</summary>
    AfterAction,

    /// <summary>A session ends.</summary>
    SessionEnd,
}
