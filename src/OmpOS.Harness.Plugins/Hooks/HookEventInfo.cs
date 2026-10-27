namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// The static facts about one hook event.
/// </summary>
/// <param name="Event">The event.</param>
/// <param name="Name">The camelCase wire name, for example <c>beforeShellExecution</c>.</param>
/// <param name="Kind">How the event treats a hook's answer.</param>
/// <param name="Honoured">The output fields the event uses.</param>
/// <param name="AskHandling">What the event does with the permission <c>ask</c>.</param>
/// <param name="FixedMatcherValue">
/// The value a matcher is tested against when it is the same for every call (for example <c>Read</c> for
/// <c>beforeReadFile</c>), or null when the value comes from the payload or the event has no matcher.
/// </param>
/// <param name="MatcherField">The payload field a matcher is tested against, or null when there is none.</param>
/// <param name="CanProduceInOmpOs">False for the two Tab events, which OmpOS cannot produce.</param>
public sealed record HookEventInfo(
    HookEvent Event,
    string Name,
    HookEventKind Kind,
    HookOutputFields Honoured,
    HookAskHandling AskHandling,
    string? FixedMatcherValue,
    string? MatcherField,
    bool CanProduceInOmpOs)
{
    /// <summary>
    /// True when exit code 2 and a <c>deny</c> answer block the action: the permission events and
    /// <c>beforeSubmitPrompt</c>.
    /// </summary>
    public bool CanBlock => Kind == HookEventKind.Permission || Event == HookEvent.BeforeSubmitPrompt;
}
