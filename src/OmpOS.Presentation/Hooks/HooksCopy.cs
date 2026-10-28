using System.Globalization;

namespace OmpOS.Presentation.Hooks;

/// <summary>
/// Every text of the Hooks page, the run log and the thread notices, in one place so the views carry no literal. No sentence holds a hook's
/// environment value or output: those are shown only after redaction, by the model that owns them.
/// </summary>
public static class HooksCopy
{
    /// <summary>The navigation label of the Hooks page.</summary>
    public const string Title = "Hooks";

    /// <summary>The heading of the Hooks page.</summary>
    public const string Heading = "Hooks";

    /// <summary>The sentence under the heading.</summary>
    public const string Description = "Hooks are your own scripts that watch and steer the agent: block a shell command, format a file after an edit, or ask the agent to keep going.";

    /// <summary>The label of the global switch.</summary>
    public const string RunHooksLabel = "Run hooks";

    /// <summary>The sentence under the global switch while it is on.</summary>
    public const string RunHooksOnDescription = "Hooks that you have approved run during agent sessions. A hook runs with your permissions.";

    /// <summary>The sentence under the global switch while it is off.</summary>
    public const string RunHooksOffDescription = "No hook runs, in any session, until you switch this on again. Sessions that are already open keep the hooks they started with.";

    /// <summary>The explanation shown instead of the list while the global switch is off.</summary>
    public const string RunHooksOffState = "Hooks are switched off. Switch Run hooks on to list and review them.";

    /// <summary>The text shown while the hooks are being read.</summary>
    public const string Loading = "Loading hooks.";

    /// <summary>The heading shown when the hooks could not be read.</summary>
    public const string ErrorHeading = "The hooks could not be loaded";

    /// <summary>The label of the button that reads the hooks again.</summary>
    public const string ReloadLabel = "Reload";

    /// <summary>The heading of the empty state.</summary>
    public const string EmptyHeading = "No hooks yet";

    /// <summary>The explanation of the empty state.</summary>
    public const string EmptyExplanation =
        "OmpOS reads the same hooks.json files as Cursor. Put a file at .ompos/hooks.json (or .cursor/hooks.json) in your project, or at ~/.ompos/hooks.json for every project, and its hooks appear here. "
        + "Nothing runs until you have reviewed and approved the exact command.";

    /// <summary>The label of the example file.</summary>
    public const string ExampleLabel = "Example hooks.json";

    /// <summary>The label of the button that copies the example file.</summary>
    public const string CopyExampleLabel = "Copy example";

    /// <summary>The announcement after the example file was copied.</summary>
    public const string ExampleCopied = "The example hooks.json was copied.";

    /// <summary>An example hooks.json that blocks one risky command. It is valid, harmless and copyable.</summary>
    public const string ExampleJson =
        "{\n"
        + "  \"version\": 1,\n"
        + "  \"hooks\": {\n"
        + "    \"beforeShellExecution\": [\n"
        + "      {\n"
        + "        \"command\": \".ompos/hooks/block-force-push.sh\",\n"
        + "        \"matcher\": \"git push .*--force\",\n"
        + "        \"timeout\": 10,\n"
        + "        \"failClosed\": true\n"
        + "      }\n"
        + "    ]\n"
        + "  }\n"
        + "}\n";

    /// <summary>The heading of the invalid-configuration state.</summary>
    public const string InvalidHeading = "The hook files are not valid";

    /// <summary>The explanation of the invalid-configuration state.</summary>
    public const string InvalidExplanation = "No hook could be read. Fix the files named below; OmpOS reads them again when you press Reload.";

    /// <summary>The heading of the diagnostics list.</summary>
    public const string DiagnosticsHeading = "Problems found in hook files";

    /// <summary>The heading of the untrusted-workspace state.</summary>
    public const string UntrustedHeading = "Trust this folder to load its hooks?";

    /// <summary>The explanation of the untrusted-workspace state.</summary>
    public const string UntrustedExplanation =
        "This folder has hook files. A hook is a program that runs on your machine, so OmpOS does not read a folder's hooks until you trust the folder. "
        + "Trusting the folder only lists the hooks here; each hook still needs your approval before it runs.";

    /// <summary>The label of the button that trusts the folder.</summary>
    public const string TrustFolderLabel = "Trust this folder";

    /// <summary>The label of the button that approves every hook that needs consent.</summary>
    public const string TrustAllLabel = "Trust all hooks in this folder";

    /// <summary>The explanation under the Trust all button.</summary>
    public const string TrustAllExplanation = "Approves every hook listed now, exactly as it is written now. If a hook changes later, it asks again.";

    /// <summary>The heading of the consent prompt.</summary>
    public const string ConsentHeading = "Review before you approve";

    /// <summary>The warning shown with every consent prompt.</summary>
    public const string ConsentWarning = "An approved hook runs with your permissions and can do anything you can do on this machine. Approve it only if you trust where it came from.";

    /// <summary>The label of the button that approves the selected hook.</summary>
    public const string ApproveLabel = "Approve this hook";

    /// <summary>The label of the button that closes the consent prompt without approving.</summary>
    public const string CloseReviewLabel = "Close review";

    /// <summary>The label of the button that withdraws consent.</summary>
    public const string RevokeLabel = "Withdraw approval";

    /// <summary>The label of the switch that turns one hook on or off.</summary>
    public const string EnabledLabel = "Enabled";

    /// <summary>The label of the button that lifts a quarantine.</summary>
    public const string LiftQuarantineLabel = "Lift quarantine";

    /// <summary>The label of the box where the reason for lifting a quarantine is typed.</summary>
    public const string LiftReasonLabel = "Why is it safe to lift the quarantine?";

    /// <summary>The text shown when a quarantine cannot be lifted without a reason.</summary>
    public const string LiftReasonRequired = "Say why the quarantine can be lifted. The reason is recorded.";

    /// <summary>The label of the field that shows the exact command.</summary>
    public const string CommandLabel = "Command";

    /// <summary>The label of the field that shows the argument list.</summary>
    public const string ArgumentsLabel = "Arguments";

    /// <summary>The label of the field that shows a prompt hook's prompt.</summary>
    public const string PromptLabel = "Prompt";

    /// <summary>The label of the matcher field.</summary>
    public const string MatcherLabel = "Matcher";

    /// <summary>The label of the timeout field.</summary>
    public const string TimeoutLabel = "Timeout";

    /// <summary>The label of the working directory field.</summary>
    public const string WorkingDirectoryLabel = "Runs in";

    /// <summary>The label of the environment field.</summary>
    public const string EnvironmentLabel = "Extra environment variables";

    /// <summary>The label of the failure policy field.</summary>
    public const string FailurePolicyLabel = "If it fails";

    /// <summary>The label of the changes list.</summary>
    public const string ChangesLabel = "Changed since you approved it";

    /// <summary>The text shown when a hook was never approved, so there is nothing to compare.</summary>
    public const string NeverApproved = "You have not approved this hook before.";

    /// <summary>The text shown when a hook is the same as the version you approved.</summary>
    public const string NoChanges = "Nothing changed since you approved it.";

    /// <summary>The text shown for a matcher that matches everything.</summary>
    public const string MatchesEverything = "Matches everything";

    /// <summary>The text shown for a hook with no extra environment variables.</summary>
    public const string NoEnvironment = "None";

    /// <summary>The text shown for a hook that has not run.</summary>
    public const string NotRunYet = "Not run yet";

    /// <summary>The text for a failure policy that lets the action through.</summary>
    public const string FailsOpen = "The action goes ahead";

    /// <summary>The text for a failure policy that blocks the action.</summary>
    public const string FailsClosed = "The action is blocked";

    /// <summary>The text for a hook that runs through a shell.</summary>
    public const string RunsThroughShell = "Runs through the shell";

    /// <summary>The text for a hook that runs without a shell.</summary>
    public const string RunsWithoutShell = "Runs directly, without a shell";

    /// <summary>The text for a prompt hook.</summary>
    public const string AsksAModel = "Asks a model. It starts no process.";

    /// <summary>The label of the run log.</summary>
    public const string RunLogHeading = "Hook run log";

    /// <summary>The sentence under the run log heading.</summary>
    public const string RunLogDescription = "The latest runs of every hook, newest first. Secrets that look like keys or tokens are removed from the output shown here.";

    /// <summary>The text shown when the run log is empty.</summary>
    public const string RunLogEmpty = "No hook has run in this session.";

    /// <summary>The text shown when the filter hides every run.</summary>
    public const string RunLogNoMatch = "No run matches the filter.";

    /// <summary>The label of the log filter.</summary>
    public const string RunLogFilterLabel = "Show";

    /// <summary>The text of the filter entry that shows every run.</summary>
    public const string RunLogAll = "All runs";

    /// <summary>The label of the button that copies the log as JSON.</summary>
    public const string CopyLogLabel = "Copy as JSON";

    /// <summary>The announcement after the log was copied.</summary>
    public const string LogCopied = "The hook run log was copied.";

    /// <summary>The label of the button that opens the log drawer.</summary>
    public const string OpenLogLabel = "Open hook log";

    /// <summary>The label of the button that closes the log drawer.</summary>
    public const string CloseLogLabel = "Close hook log";

    /// <summary>The heading of the output part of a log row.</summary>
    public const string OutputLabel = "Output";

    /// <summary>The heading of the error output part of a log row.</summary>
    public const string ErrorOutputLabel = "Error output";

    /// <summary>The text appended to an output that was cut.</summary>
    public const string OutputCut = "The output was cut.";

    /// <summary>The summary of a collapsible context row in the thread.</summary>
    public const string ContextSummary = "Hook context";

    /// <summary>The label of the thread notice that asks for a consent review.</summary>
    public const string ReviewLabel = "Review hook";

    /// <summary>Returns the heading of a scope group.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The heading.</returns>
    public static string ScopeTitle(HookScopeKind scope) => scope switch
    {
        HookScopeKind.Managed => "Managed by your organisation",
        HookScopeKind.Project => "This project",
        HookScopeKind.User => "Your user hooks",
        HookScopeKind.ClaudeLocal => "Claude local settings",
        HookScopeKind.ClaudeProject => "Claude project settings",
        HookScopeKind.ClaudeUser => "Claude user settings",
        HookScopeKind.Plugin => "Plugins",
        _ => "Other",
    };

    /// <summary>Returns the word for the state of a hook row.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The word.</returns>
    public static string StateText(HookRowState state) => state switch
    {
        HookRowState.NotApplicable => "Not available",
        HookRowState.UnknownEvent => "Unknown event",
        HookRowState.Quarantined => "Quarantined",
        HookRowState.Disabled => "Disabled",
        HookRowState.NeedsConsent => "Needs your approval",
        HookRowState.Running => "Running",
        HookRowState.LastRunFailed => "Last run failed",
        _ => "Ready",
    };

    /// <summary>Returns the sentence that explains the state of a hook row.</summary>
    /// <param name="state">The state.</param>
    /// <param name="item">The hook.</param>
    /// <returns>The sentence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public static string StateDetail(HookRowState state, HookItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return state switch
        {
            HookRowState.NotApplicable => "OmpOS has no Tab inline completion, so this event never happens and the hook never runs.",
            HookRowState.UnknownEvent => "OmpOS does not know this event name. The hook is kept in case a newer version knows it, and it does not run.",
            HookRowState.Quarantined => string.Create(CultureInfo.InvariantCulture, $"Its source is quarantined: {item.QuarantineReason}. Hooks that guard an action block it until you lift the quarantine."),
            HookRowState.Disabled => "The hook is switched off and does not run.",
            HookRowState.NeedsConsent => item.IsGuarding
                ? "No approval is recorded for this exact hook. It guards an action, so that action is blocked until you approve it."
                : "No approval is recorded for this exact hook, so it is skipped until you approve it.",
            HookRowState.Running => "The hook is running now.",
            HookRowState.LastRunFailed => "The last run did not finish well. Open the run log for the details.",
            _ => "Approved and ready.",
        };
    }

    /// <summary>Returns the word for the outcome of a run.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The word.</returns>
    public static string OutcomeText(HookRunOutcome outcome) => outcome switch
    {
        HookRunOutcome.Succeeded => "Succeeded",
        HookRunOutcome.Denied => "Denied",
        HookRunOutcome.Failed => "Failed",
        HookRunOutcome.TimedOut => "Timed out",
        HookRunOutcome.Skipped => "Skipped",
        HookRunOutcome.Faulted => "Could not start",
        HookRunOutcome.AwaitingConsent => "Waiting for approval",
        HookRunOutcome.Quarantined => "Quarantined",
        HookRunOutcome.InvalidResponse => "Invalid answer",
        _ => "Unknown",
    };

    /// <summary>Returns the last-run text of a hook row.</summary>
    /// <param name="run">The run, or null when the hook has not run.</param>
    /// <returns>The text: the outcome and, when the hook ran, how long it took.</returns>
    public static string LastRunText(HookRunItem? run) => run is null
        ? NotRunYet
        : run.Outcome is HookRunOutcome.Succeeded or HookRunOutcome.Denied or HookRunOutcome.Failed or HookRunOutcome.TimedOut or HookRunOutcome.InvalidResponse
            ? string.Create(CultureInfo.InvariantCulture, $"{OutcomeText(run.Outcome)} in {run.DurationMs} ms")
            : OutcomeText(run.Outcome);

    /// <summary>Returns the text for a shell flag.</summary>
    /// <param name="item">The hook.</param>
    /// <returns>Whether the hook runs through a shell, without a shell, or asks a model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public static string ShellText(HookItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsPrompt)
        {
            return AsksAModel;
        }

        return item.ShellName is { Length: > 0 } shell
            ? string.Create(CultureInfo.InvariantCulture, $"{RunsThroughShell} ({shell})")
            : RunsWithoutShell;
    }

    /// <summary>Returns the timeout text.</summary>
    /// <param name="seconds">The timeout in seconds.</param>
    /// <returns>The text.</returns>
    public static string TimeoutText(int seconds) => string.Create(CultureInfo.InvariantCulture, $"{seconds} s");

    /// <summary>Returns the text of an environment name list.</summary>
    /// <param name="names">The variable names.</param>
    /// <returns>The names joined, or <see cref="NoEnvironment"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is null.</exception>
    public static string EnvironmentText(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return names.Count == 0 ? NoEnvironment : string.Join(", ", names);
    }

    /// <summary>Returns the text of a matcher.</summary>
    /// <param name="matcher">The matcher, or null.</param>
    /// <returns>The matcher, or <see cref="MatchesEverything"/>.</returns>
    public static string MatcherText(string? matcher) =>
        string.IsNullOrEmpty(matcher) || string.Equals(matcher, "*", StringComparison.Ordinal) ? MatchesEverything : matcher;

    /// <summary>Returns the accessible name of a hook row.</summary>
    /// <param name="eventName">The event.</param>
    /// <param name="displayName">The hook's name.</param>
    /// <param name="stateText">The state word.</param>
    /// <returns>The name.</returns>
    public static string RowName(string eventName, string displayName, string stateText) =>
        string.Create(CultureInfo.InvariantCulture, $"Hook {displayName}, event {eventName}: {stateText}");

    /// <summary>Returns the announcement after a hook was approved.</summary>
    /// <param name="name">The hook's name.</param>
    /// <returns>The sentence.</returns>
    public static string ApprovedAnnouncement(string name) => string.Create(CultureInfo.InvariantCulture, $"Approved {name}.");

    /// <summary>Returns the announcement after approval was withdrawn.</summary>
    /// <param name="name">The hook's name.</param>
    /// <returns>The sentence.</returns>
    public static string RevokedAnnouncement(string name) => string.Create(CultureInfo.InvariantCulture, $"Withdrew approval for {name}.");

    /// <summary>Returns the announcement after a hook was switched on or off.</summary>
    /// <param name="name">The hook's name.</param>
    /// <param name="enabled">True when it was switched on.</param>
    /// <returns>The sentence.</returns>
    public static string EnabledAnnouncement(string name, bool enabled) =>
        string.Create(CultureInfo.InvariantCulture, $"{name} is now {(enabled ? "on" : "off")}.");

    /// <summary>Returns the announcement after several hooks were approved.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The sentence.</returns>
    public static string ApprovedAllAnnouncement(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"Approved {count} {(count == 1 ? "hook" : "hooks")}.");

    /// <summary>Returns the text of a thread notice that says a hook denied an action.</summary>
    /// <param name="hookName">The hook's name, or an empty string when unknown.</param>
    /// <param name="message">The hook's message to the user, or an empty string.</param>
    /// <returns>The sentence.</returns>
    public static string DeniedNotice(string hookName, string message)
    {
        var by = string.IsNullOrWhiteSpace(hookName) ? "denied by a hook" : string.Create(CultureInfo.InvariantCulture, $"denied by hook {hookName}");
        return string.IsNullOrWhiteSpace(message) ? by : string.Create(CultureInfo.InvariantCulture, $"{by}: {message}");
    }

    /// <summary>Returns the text of a thread notice that says a stop hook continued the conversation.</summary>
    /// <param name="count">How many follow-ups the conversation has had since your last message.</param>
    /// <param name="limit">The hook's follow-up cap, or null when it has none.</param>
    /// <returns>The sentence.</returns>
    public static string FollowUpNotice(int count, int? limit) => limit is { } cap
        ? string.Create(CultureInfo.InvariantCulture, $"Hook continued the conversation ({count} of {cap})")
        : string.Create(CultureInfo.InvariantCulture, $"Hook continued the conversation ({count})");

    /// <summary>Returns the text of a thread notice that says a guarding hook has no approval.</summary>
    /// <param name="hookName">The hook's name, or an empty string when unknown.</param>
    /// <returns>The sentence.</returns>
    public static string ConsentNotice(string hookName) => string.IsNullOrWhiteSpace(hookName)
        ? "A hook guards this action and has no approval, so the action was blocked."
        : string.Create(CultureInfo.InvariantCulture, $"Hook {hookName} guards this action and has no approval, so the action was blocked.");
}
