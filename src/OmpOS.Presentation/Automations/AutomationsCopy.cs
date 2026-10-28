using System.Globalization;

namespace OmpOS.Presentation.Automations;

/// <summary>
/// Every text of the hosted automations page and its editor, in one place so the views carry no literal. No sentence holds a webhook
/// secret, a prompt or a typed entry; a sentence about an entry never repeats what the user typed. Names of automations appear only in
/// accessible names and notices, never in field messages.
/// </summary>
public static class AutomationsCopy
{
    // Page.

    /// <summary>The navigation label of the Automations page.</summary>
    public const string Title = "Automations";

    /// <summary>The heading of the Automations page.</summary>
    public const string Heading = "Automations";

    /// <summary>The sentence under the heading.</summary>
    public const string Description = "Run a prompt on a schedule or when a signed webhook arrives, in a local session or on a cloud agent.";

    /// <summary>The label of the button that opens the editor for a new automation.</summary>
    public const string NewAutomationLabel = "New automation";

    /// <summary>The label of the button that reads the list again.</summary>
    public const string RefreshLabel = "Refresh";

    /// <summary>The label of the button that repeats a failed load.</summary>
    public const string TryAgainLabel = "Try again";

    /// <summary>The text shown while the list loads.</summary>
    public const string Loading = "Loading automations.";

    /// <summary>The heading of the state in which the service has automations switched off.</summary>
    public const string UnavailableHeading = "Automations are switched off";

    /// <summary>What the unavailable state says and how to switch automations on.</summary>
    public const string UnavailableBody = "This service has automations switched off. To turn them on, set the service setting Automations:Enabled to true and restart the service.";

    /// <summary>The heading of the state in which the list failed to load.</summary>
    public const string LoadFailedHeading = "Automations could not be loaded";

    /// <summary>The heading of the state in which no automation exists.</summary>
    public const string EmptyHeading = "No automations yet";

    /// <summary>What the empty state says.</summary>
    public const string EmptyBody = "An automation runs a prompt for you on a schedule or when a webhook arrives. Create one to get started.";

    /// <summary>The accessible name of the list of automations.</summary>
    public const string ListName = "Automations";

    /// <summary>The text shown while no automation is selected.</summary>
    public const string NoSelection = "Select an automation to see its details and run history.";

    /// <summary>The heading of the details of the selected automation.</summary>
    public const string DetailsHeading = "Details";

    /// <summary>The heading of the run history.</summary>
    public const string HistoryLabel = "Run history";

    /// <summary>The text shown while the selected automation has no run.</summary>
    public const string NoHistory = "This automation has not run yet.";

    /// <summary>The label of the button that starts a run now.</summary>
    public const string RunNowLabel = "Run now";

    /// <summary>The label of the button that opens the editor.</summary>
    public const string EditLabel = "Edit";

    /// <summary>The label of the button that starts a delete.</summary>
    public const string DeleteLabel = "Delete";

    /// <summary>The label of the button that confirms a delete.</summary>
    public const string ConfirmDeleteLabel = "Delete automation";

    /// <summary>The label of the button that cancels a delete or closes the editor.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>The accessible name of the actions of the selected automation.</summary>
    public const string ActionsName = "Automation actions";

    /// <summary>The accessible name of the delete confirmation.</summary>
    public const string DeleteConfirmationName = "Confirm deleting the automation";

    /// <summary>The accessible name of the failure message of an action.</summary>
    public const string ActionErrorName = "Automation action failed";

    /// <summary>The accessible name of the polite announcement of a finished action.</summary>
    public const string NoticeName = "Automation action finished";

    /// <summary>The label of the webhook path line.</summary>
    public const string WebhookPathLabel = "Webhook path";

    /// <summary>The text of a run row that has no summary.</summary>
    public const string NoSummary = "No summary.";

    // Row texts.

    /// <summary>The trigger text of an automation that has no trigger.</summary>
    public const string NoTriggers = "No triggers";

    /// <summary>The target text of a local automation.</summary>
    public const string LocalTarget = "Local session";

    /// <summary>The target text of a cloud automation that names no repository.</summary>
    public const string CloudTargetNoRepository = "Cloud agent";

    /// <summary>The next run text of an automation that is not scheduled.</summary>
    public const string NextRunNotScheduled = "Next run: not scheduled";

    /// <summary>The last result text of an automation that has not run.</summary>
    public const string LastRunNever = "Last run: never";

    /// <summary>The text of the enable toggle of an enabled automation.</summary>
    public const string TurnOffLabel = "Turn off";

    /// <summary>The text of the enable toggle of a disabled automation.</summary>
    public const string TurnOnLabel = "Turn on";

    /// <summary>The duration text of a run in progress.</summary>
    public const string DurationRunning = "running";

    // Editor.

    /// <summary>The heading of the editor of a new automation.</summary>
    public const string EditorNewTitle = "New automation";

    /// <summary>The heading of the editor of an existing automation.</summary>
    public const string EditorEditTitle = "Edit automation";

    /// <summary>The label of the name field.</summary>
    public const string NameLabel = "Name";

    /// <summary>The label of the prompt field.</summary>
    public const string PromptLabel = "Prompt";

    /// <summary>The help under the prompt field.</summary>
    public const string PromptHelp = "What the agent is asked to do each time the automation runs.";

    /// <summary>The label of the enabled checkbox.</summary>
    public const string EnabledLabel = "Enabled";

    /// <summary>The label of the time zone field.</summary>
    public const string TimeZoneLabel = "Time zone";

    /// <summary>The help under the time zone field.</summary>
    public const string TimeZoneHelp = "An IANA time zone name, for example UTC or Europe/Berlin. Schedules are read in this zone.";

    /// <summary>The heading of the triggers section.</summary>
    public const string TriggersHeading = "Triggers";

    /// <summary>The label of the cron expression field.</summary>
    public const string CronLabel = "Schedule (cron expression)";

    /// <summary>The help under the cron expression field.</summary>
    public const string CronHelp = "Minute, hour, day of month, month and day of week. Leave empty for no schedule.";

    /// <summary>The label of the button that previews the next runs of the schedule.</summary>
    public const string PreviewCronLabel = "Preview next runs";

    /// <summary>The heading of the list of previewed runs.</summary>
    public const string CronPreviewHeading = "Next runs";

    /// <summary>The accessible name of the previewed runs.</summary>
    public const string CronPreviewName = "Next scheduled runs";

    /// <summary>The label of the checkbox that turns the webhook trigger on.</summary>
    public const string WebhookEnabledLabel = "Run when a signed webhook arrives";

    /// <summary>The label of the webhook type pick list.</summary>
    public const string WebhookFlavourLabel = "Webhook type";

    /// <summary>The label of the generic webhook type.</summary>
    public const string FlavourGenericLabel = "Generic";

    /// <summary>The label of the GitHub webhook type.</summary>
    public const string FlavourGithubLabel = "GitHub";

    /// <summary>The label of the GitHub events field.</summary>
    public const string WebhookEventsLabel = "GitHub events";

    /// <summary>The help under the GitHub events field.</summary>
    public const string WebhookEventsHelp = "Comma separated, for example push, pull_request.";

    /// <summary>The label of the GitHub actions field.</summary>
    public const string WebhookActionsLabel = "GitHub actions";

    /// <summary>The help under the GitHub actions field.</summary>
    public const string WebhookActionsHelp = "Comma separated, for example opened, synchronize.";

    /// <summary>The label of the branches field.</summary>
    public const string WebhookBranchesLabel = "Branches";

    /// <summary>The help under the branches field.</summary>
    public const string WebhookBranchesHelp = "Comma separated branch names or globs, for example main, release/*.";

    /// <summary>The heading of the target section.</summary>
    public const string TargetHeading = "Target";

    /// <summary>The label of the target kind pick list.</summary>
    public const string TargetKindLabel = "Run on";

    /// <summary>The label of the local target kind.</summary>
    public const string TargetLocalLabel = "Local session";

    /// <summary>The label of the cloud target kind.</summary>
    public const string TargetCloudLabel = "Cloud agent";

    /// <summary>The label of the repositories field.</summary>
    public const string RepositoriesLabel = "Repositories";

    /// <summary>The help under the repositories field.</summary>
    public const string RepositoriesHelp = "One repository per line: an https address, then optionally a branch and a base branch, separated by spaces. Write - for no branch when you give a base branch.";

    /// <summary>The label of the secret names field.</summary>
    public const string SecretRefsLabel = "Secret names";

    /// <summary>The help under the secret names field.</summary>
    public const string SecretRefsHelp = "Comma separated names of stored secrets the cloud agent may read. Names only, never values.";

    /// <summary>The label of the environment field.</summary>
    public const string EnvironmentLabel = "Environment";

    /// <summary>The label of the model field.</summary>
    public const string ModelLabel = "Model";

    /// <summary>The label of the mode field.</summary>
    public const string ModeLabel = "Mode";

    /// <summary>The help under the environment, model and mode fields.</summary>
    public const string HostDefaultHelp = "Leave empty to use the default of the host.";

    /// <summary>The label of the group of pull request actions.</summary>
    public const string AllowedToolsLabel = "Pull request actions a run may take";

    /// <summary>The label of the checkbox that allows opening pull requests.</summary>
    public const string AllowCreateLabel = "Open pull requests";

    /// <summary>The label of the checkbox that allows commenting on pull requests.</summary>
    public const string AllowCommentLabel = "Comment on pull requests";

    /// <summary>The label of the checkbox that allows requesting reviewers.</summary>
    public const string AllowReviewersLabel = "Request reviewers";

    /// <summary>The heading of the limits section.</summary>
    public const string LimitsHeading = "Limits";

    /// <summary>The label of the concurrent runs field.</summary>
    public const string MaxConcurrentLabel = "Concurrent runs (1 to 4)";

    /// <summary>The label of the runs per hour field.</summary>
    public const string MaxRunsPerHourLabel = "Runs per hour (1 to 60)";

    /// <summary>The label of the maximum runtime field.</summary>
    public const string MaxRuntimeLabel = "Maximum runtime in minutes (1 to 360)";

    /// <summary>The label of the Save button.</summary>
    public const string SaveLabel = "Save";

    /// <summary>The announcement while the automation is being saved.</summary>
    public const string Saving = "Saving the automation.";

    /// <summary>The accessible name of the failure message of a save.</summary>
    public const string SaveFailureName = "Saving the automation failed";

    /// <summary>The accessible name of a field message.</summary>
    public const string FieldProblemName = "Problem with the automation";

    // Webhook secret.

    /// <summary>The label of the button that replaces the webhook secret.</summary>
    public const string RotateSecretLabel = "Rotate webhook secret";

    /// <summary>The heading of the panel that shows a new webhook secret.</summary>
    public const string SecretHeading = "Webhook secret";

    /// <summary>What the secret panel says: the secret is shown once.</summary>
    public const string SecretBody = "Copy this secret now and store it somewhere safe. It is shown only once and cannot be read again.";

    /// <summary>The accessible name of the box that shows the secret.</summary>
    public const string SecretValueName = "Webhook secret value";

    /// <summary>The label of the button that copies the secret.</summary>
    public const string CopySecretLabel = "Copy secret";

    /// <summary>The announcement after the secret was copied.</summary>
    public const string SecretCopied = "The secret was copied to the clipboard.";

    /// <summary>The label of the button that confirms the secret was stored.</summary>
    public const string StoredSecretLabel = "I stored it";

    // Validation messages. None repeats what the user typed.

    /// <summary>The message of an empty name.</summary>
    public const string NameRequired = "Enter a name.";

    /// <summary>The message of a name over 100 characters.</summary>
    public const string NameTooLong = "The name is longer than 100 characters.";

    /// <summary>The message of an empty prompt.</summary>
    public const string PromptRequired = "Enter a prompt.";

    /// <summary>The message of a prompt over 20000 characters.</summary>
    public const string PromptTooLong = "The prompt is longer than 20000 characters.";

    /// <summary>The message of an automation without trigger.</summary>
    public const string TriggersRequired = "Add a schedule or turn on the webhook trigger so the automation can start.";

    /// <summary>The message of an automation without a schedule, on a head that receives no webhooks.</summary>
    public const string ScheduleRequired = "Add a schedule so the automation can start.";

    /// <summary>The sentence of the editor of a head that receives no webhooks: the schedule is the only trigger.</summary>
    public const string ScheduleOnlyNote = "Automations of this computer start from their schedule only. A webhook needs the OmpOS service, which has a public address; create that automation there.";

    /// <summary>The sentence of the desktop page: where local automations run, and when.</summary>
    public const string DesktopNote = "A local automation starts a new agent session in the open workspace at its scheduled time, while OmpOS is open. The first run of each automation asks you to confirm it, and the run mode of the session applies.";

    /// <summary>The sentence of the desktop page when omp is not installed: a local automation cannot run.</summary>
    public const string NoOmpNote = "omp was not found, so local automations cannot run. Install omp, then restart OmpOS.";

    /// <summary>The message of a webhook trigger saved on a head that receives none.</summary>
    public const string WebhookNotSupported = "This computer does not receive webhooks. Remove the webhook trigger or create the automation in the OmpOS service.";

    /// <summary>The message of a local automation that was not confirmed.</summary>
    public const string NotConfirmed = "The first run was not confirmed, so nothing started.";

    /// <summary>The title of the confirmation card that appears in the session of the first run of an automation.</summary>
    public const string ConsentCardTitle = "Run automation";

    /// <summary>The failure of a run that started before the head had opened its agent sessions.</summary>
    public const string SessionsNotReady = "OmpOS had not finished opening, so no agent session could be started.";

    /// <summary>The failure of a run whose agent turn ended without a result.</summary>
    public const string TurnFailed = "The agent's turn ended with an error.";

    /// <summary>The failure of a cloud run on a head without a cloud agents service.</summary>
    public const string CloudNotConfigured = "Cloud agents are not set up on this computer, so a cloud automation cannot run. Set the OMPOS_API_BASE_URL variable and restart OmpOS.";

    /// <summary>The message of a save for a target the head cannot run.</summary>
    public const string TargetUnavailable = "This computer cannot run that kind of target.";

    /// <summary>The sentence of the confirmation card of a first run.</summary>
    /// <param name="name">The name of the automation.</param>
    /// <param name="workspaceRoot">The workspace the run works in.</param>
    /// <returns>The sentence.</returns>
    public static string ConsentSummary(string name, string workspaceRoot) =>
        "The automation \"" + name + "\" starts an agent session in " + workspaceRoot + " with its saved prompt, now and on its schedule. Allow it to run on its own; the run mode still decides which tools the agent may use.";

    /// <summary>The message of a cron expression whose preview reported it never runs.</summary>
    public const string CronNeverRuns = "This schedule never runs.";

    /// <summary>The message of a webhook type that is neither generic nor GitHub.</summary>
    public const string WebhookFlavourInvalid = "Choose a webhook type.";

    /// <summary>The message of an event, action or branch filter that is not valid.</summary>
    public const string WebhookFilterInvalid = "A webhook filter is not valid: events and actions hold letters, digits, underscores, dashes and dots, and branch names hold no spaces.";

    /// <summary>The message of a target kind this application cannot run.</summary>
    public const string TargetUnsupported = "This application cannot run automations on that target. Choose another target.";

    /// <summary>The message of a cloud target without repository.</summary>
    public const string RepositoriesRequired = "Add at least one repository.";

    /// <summary>The message of a cloud target with more than 16 repositories.</summary>
    public const string RepositoriesTooMany = "Use at most 16 repositories.";

    /// <summary>The message of a repository entry that is not valid.</summary>
    public const string RepositoryInvalid = "Every repository must be an https address without spaces or credentials, optionally followed by a branch and a base branch.";

    /// <summary>The message of a secret name that is not valid.</summary>
    public const string SecretNameInvalid = "A secret name starts with a letter or an underscore and holds only letters, digits and underscores, up to 64 characters.";

    /// <summary>The message of a concurrent runs entry that is not valid.</summary>
    public const string ConcurrentRunsInvalid = "Concurrent runs must be a whole number from 1 to 4.";

    /// <summary>The message of a runs per hour entry that is not valid.</summary>
    public const string RunsPerHourInvalid = "Runs per hour must be a whole number from 1 to 60.";

    /// <summary>The message of a maximum runtime entry that is not valid.</summary>
    public const string RuntimeInvalid = "Maximum runtime must be a whole number of minutes from 1 to 360.";

    /// <summary>The start of the line that says which fields keep Save unavailable.</summary>
    public const string SaveBlockedPrefix = "Save is available when these fields are complete: ";

    // Names of the editor's fields, as the Errors dictionary keys them.

    /// <summary>The error key of the name field.</summary>
    public const string FieldName = "name";

    /// <summary>The error key of the prompt field.</summary>
    public const string FieldPrompt = "prompt";

    /// <summary>The error key of the trigger set.</summary>
    public const string FieldTriggers = "triggers";

    /// <summary>The error key of the cron expression.</summary>
    public const string FieldCron = "cron";

    /// <summary>The error key of the webhook settings.</summary>
    public const string FieldWebhook = "webhook";

    /// <summary>The error key of the target kind.</summary>
    public const string FieldTarget = "target";

    /// <summary>The error key of the repositories.</summary>
    public const string FieldRepositories = "repositories";

    /// <summary>The error key of the secret names.</summary>
    public const string FieldSecrets = "secrets";

    /// <summary>The error key of the limits.</summary>
    public const string FieldLimits = "limits";

    /// <summary>Gets the accessible name of the enable toggle of an automation.</summary>
    /// <param name="name">The automation name.</param>
    /// <param name="enabled">Whether the automation is enabled now; the toggle names the change it makes.</param>
    /// <returns>The accessible name.</returns>
    public static string ToggleName(string name, bool enabled) =>
        (enabled ? "Turn off automation " : "Turn on automation ") + name;

    /// <summary>Gets the accessible name of the Run now button of an automation.</summary>
    /// <param name="name">The automation name.</param>
    /// <returns>The accessible name.</returns>
    public static string RunNowName(string name) => "Run automation " + name + " now";

    /// <summary>Gets the accessible name of the Edit button of an automation.</summary>
    /// <param name="name">The automation name.</param>
    /// <returns>The accessible name.</returns>
    public static string EditName(string name) => "Edit automation " + name;

    /// <summary>Gets the accessible name of the Delete button of an automation.</summary>
    /// <param name="name">The automation name.</param>
    /// <returns>The accessible name.</returns>
    public static string DeleteName(string name) => "Delete automation " + name;

    /// <summary>Gets the accessible name of the button that confirms deleting an automation.</summary>
    /// <param name="name">The automation name.</param>
    /// <returns>The accessible name.</returns>
    public static string ConfirmDeleteName(string name) => "Confirm deleting automation " + name;

    /// <summary>Gets the sentence of the delete confirmation.</summary>
    /// <param name="name">The automation name.</param>
    /// <returns>The warning.</returns>
    public static string DeleteWarning(string name) => $"Delete {name}? It will no longer run and cannot be recovered.";

    /// <summary>Gets the announcement after an automation was saved.</summary>
    /// <param name="name">The automation name.</param>
    /// <returns>The announcement.</returns>
    public static string SavedNotice(string name) => $"Saved automation {name}.";

    /// <summary>Gets the announcement after an automation was deleted.</summary>
    /// <param name="name">The automation name.</param>
    /// <returns>The announcement.</returns>
    public static string DeletedNotice(string name) => $"Deleted automation {name}.";

    /// <summary>Gets the announcement after a run was started by hand.</summary>
    /// <param name="name">The automation name.</param>
    /// <param name="status">The status the started run reported.</param>
    /// <returns>The announcement.</returns>
    public static string RunNotice(string name, HostedRunStatus status) => $"Run of {name}: {StatusWord(status)}.";

    /// <summary>Gets the label of a target kind.</summary>
    /// <param name="kind">The target kind, <c>local</c> or <c>cloud</c>.</param>
    /// <returns>The label; an unknown kind is returned unchanged.</returns>
    public static string TargetKindText(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        return kind switch
        {
            "local" => TargetLocalLabel,
            "cloud" => TargetCloudLabel,
            _ => kind,
        };
    }

    /// <summary>Gets the label of a field, as the line that says what keeps Save unavailable names it.</summary>
    /// <param name="field">The error key of the field.</param>
    /// <returns>The label; an unknown key is returned unchanged.</returns>
    public static string FieldText(string field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return field switch
        {
            FieldName => NameLabel,
            FieldPrompt => PromptLabel,
            FieldTriggers => TriggersHeading,
            FieldCron => "Schedule",
            FieldWebhook => "Webhook",
            FieldTarget => TargetHeading,
            FieldRepositories => RepositoriesLabel,
            FieldSecrets => SecretRefsLabel,
            FieldLimits => LimitsHeading,
            _ => field,
        };
    }

    /// <summary>Gets the lower-case word for a run status, as it reads inside a sentence.</summary>
    /// <param name="status">The run status.</param>
    /// <returns>The word.</returns>
    public static string StatusWord(HostedRunStatus status) => status switch
    {
        HostedRunStatus.Running => "running",
        HostedRunStatus.Succeeded => "succeeded",
        HostedRunStatus.Launched => "launched",
        HostedRunStatus.Failed => "failed",
        HostedRunStatus.TimedOut => "timed out",
        HostedRunStatus.Cancelled => "cancelled",
        HostedRunStatus.Skipped => "skipped",
        _ => "unknown",
    };

    /// <summary>Gets the capitalised word for a run status, as it stands alone in a list.</summary>
    /// <param name="status">The run status.</param>
    /// <returns>The word.</returns>
    public static string StatusTitle(HostedRunStatus status)
    {
        var word = StatusWord(status);
        return new string(char.ToUpperInvariant(word[0]), 1) + word[1..];
    }

    /// <summary>Gets the sentence that says what started a run.</summary>
    /// <param name="trigger">The trigger of the run: <c>cron</c>, <c>webhook</c> or <c>manual</c>.</param>
    /// <returns>The sentence; an unknown trigger is named as it is.</returns>
    public static string TriggerSentence(string trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger switch
        {
            "cron" => "Started by the schedule",
            "webhook" => "Started by a webhook",
            "manual" => "Started by hand",
            _ => "Started by " + trigger,
        };
    }

    /// <summary>Gets the line that numbers one previewed run.</summary>
    /// <param name="number">The one-based position of the run.</param>
    /// <param name="at">When the run starts.</param>
    /// <returns>The line, for example <c>1. 2026-11-01 09:00 UTC</c>.</returns>
    public static string PreviewLine(int number, DateTimeOffset at) =>
        number.ToString(CultureInfo.InvariantCulture) + ". " + FormatMinute(at);

    /// <summary>Formats a point in time to the minute, in UTC, with the same format everywhere on the page.</summary>
    /// <param name="at">The time.</param>
    /// <returns>The text, for example <c>2026-11-01 09:00 UTC</c>.</returns>
    public static string FormatMinute(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";

    /// <summary>Formats a point in time to the second, in UTC, with the same format everywhere on the page.</summary>
    /// <param name="at">The time.</param>
    /// <returns>The text, for example <c>2026-11-01 09:00:05 UTC</c>.</returns>
    public static string FormatSecond(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
}
