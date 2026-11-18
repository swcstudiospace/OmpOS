using System.Text.Json;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// What every hook of one dispatch shares.
/// </summary>
/// <param name="EventName">The event name sent as <c>hook_event_name</c>.</param>
/// <param name="Event">The Cursor event, or null for an <c>omp</c> section dispatch.</param>
/// <param name="Info">The facts about <paramref name="Event"/>, or null.</param>
/// <param name="Session">The conversation.</param>
/// <param name="Payload">The event-specific fields.</param>
/// <param name="MatcherValue">The value matchers are tested against, or null.</param>
/// <param name="CanBlock">True when exit code 2 and a failure policy can block this dispatch: a permission or prompt event, or a guarding <c>omp</c> event.</param>
internal sealed record HookDispatchContext(
    string EventName,
    HookEvent? Event,
    HookEventInfo? Info,
    HookSession Session,
    JsonElement Payload,
    string? MatcherValue,
    bool CanBlock)
{
    /// <summary>True for <c>stop</c> and <c>subagentStop</c>, which have follow-ups and a loop counter.</summary>
    public bool HasFollowups => Event is HookEvent.Stop or HookEvent.SubagentStop;
}
