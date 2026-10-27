namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Creates the <see cref="HookStep"/> and <see cref="HookRunRecord"/> values of one hook.
/// </summary>
internal static class HookStepFactory
{
    private const int MaxMatchedValueLength = 128;

    /// <summary>
    /// Creates a step for a hook that was considered but did not run.
    /// </summary>
    public static HookStep NotRun(
        EffectiveHook hook,
        HookDispatchContext context,
        DateTimeOffset at,
        HookRunStatus status,
        string note,
        HookDecisionCause denyCause = HookDecisionCause.None,
        string? denyMessage = null) =>
        new(
            Record(hook, context, at, status, denyCause == HookDecisionCause.None ? "none" : "deny", null, TimeSpan.Zero, denyMessage, null, null, false, null, note),
            null,
            denyCause,
            denyMessage,
            denyMessage,
            false);

    /// <summary>
    /// Creates a record.
    /// </summary>
    public static HookRunRecord Record(
        EffectiveHook hook,
        HookDispatchContext context,
        DateTimeOffset at,
        HookRunStatus status,
        string decision,
        int? exitCode,
        TimeSpan duration,
        string? userMessage,
        string? standardOutput,
        string? standardError,
        bool truncated,
        string? payloadSha256,
        string? note) =>
        new(
            at,
            context.Session.ConversationId,
            context.Session.GenerationId,
            context.EventName,
            hook.Id,
            hook.PluginId,
            hook.Scope,
            RecordedMatcherValue(context),
            status,
            decision,
            exitCode,
            duration,
            userMessage,
            HookRunLog.Excerpt(standardOutput),
            HookRunLog.Excerpt(standardError),
            truncated,
            payloadSha256,
            note);

    // The shell events match on the whole command line, which can hold secrets, so the log keeps only short category values.
    private static string? RecordedMatcherValue(HookDispatchContext context) =>
        context.Event is HookEvent.BeforeShellExecution or HookEvent.AfterShellExecution || context.MatcherValue is null
            ? null
            : context.MatcherValue.Length <= MaxMatchedValueLength ? context.MatcherValue : context.MatcherValue[..MaxMatchedValueLength];
}
