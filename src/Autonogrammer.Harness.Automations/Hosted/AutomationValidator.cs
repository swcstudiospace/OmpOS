using Autonogrammer.Harness.Automations.Cron;
using Autonogrammer.Harness.Automations.Scheduling;

namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Checks a <see cref="UserAutomation"/> against the model's rules and returns every problem at once, so an editor can show them together.
/// </summary>
public static class AutomationValidator
{
    private static readonly string[] GitHubEvents = ["push", "pull_request"];

    /// <summary>Validates a definition.</summary>
    /// <param name="automation">The definition.</param>
    /// <param name="apiHost">True on the API host, which has no local target and no session target.</param>
    /// <param name="isolation">The isolation level. Null means not isolated, which is the level until phase 76.</param>
    /// <returns>The problems; empty when the definition is acceptable.</returns>
    public static IReadOnlyList<AutomationValidationIssue> Validate(
        UserAutomation automation,
        bool apiHost = false,
        ISessionIsolationLevel? isolation = null)
    {
        ArgumentNullException.ThrowIfNull(automation);
        var issues = new List<AutomationValidationIssue>();
        ValidateText(automation, issues);
        ValidateTriggers(automation, issues);
        ValidateTarget(automation, issues, apiHost, isolation ?? DefaultSessionIsolation.Instance);
        ValidateTools(automation, issues);
        ValidateLimits(automation, issues);
        return issues;
    }

    private static void ValidateText(UserAutomation automation, List<AutomationValidationIssue> issues)
    {
        var name = automation.Name.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > AutomationLimits.MaxNameLength || name.Any(char.IsControl))
        {
            issues.Add(new AutomationValidationIssue("name", "The name is required, at most 100 characters, with no control characters."));
        }

        if (string.IsNullOrWhiteSpace(automation.Prompt) || automation.Prompt.Length > AutomationLimits.MaxPromptLength)
        {
            issues.Add(new AutomationValidationIssue("prompt", "The prompt is required and at most 20000 characters."));
        }

        if (!AutomationTimeZones.TryResolve(automation.TimeZone, out _))
        {
            issues.Add(new AutomationValidationIssue("timeZone", "The time zone is not known to this host. Use an IANA name such as Europe/Berlin, or UTC."));
        }
    }

    private static void ValidateTriggers(UserAutomation automation, List<AutomationValidationIssue> issues)
    {
        var triggers = automation.Triggers;
        if (triggers is not { Count: > 0 and <= AutomationLimits.MaxTriggers })
        {
            issues.Add(new AutomationValidationIssue("triggers", "Give between 1 and 8 triggers."));
            return;
        }

        var seenFlavours = new HashSet<WebhookFlavour>();
        foreach (var trigger in triggers)
        {
            if (trigger is null)
            {
                issues.Add(new AutomationValidationIssue("triggers", "A trigger is empty."));
            }
            else if (trigger.Kind == AutomationTriggerKind.Cron)
            {
                if (!CronExpression.TryParse(trigger.Cron, out _, out var error))
                {
                    issues.Add(new AutomationValidationIssue("cron", error));
                }
            }
            else
            {
                ValidateWebhook(trigger, seenFlavours, issues);
            }
        }
    }

    private static void ValidateWebhook(AutomationTriggerSpec trigger, HashSet<WebhookFlavour> seenFlavours, List<AutomationValidationIssue> issues)
    {
        if (!seenFlavours.Add(trigger.Flavour))
        {
            issues.Add(new AutomationValidationIssue("webhook", "Use at most one webhook trigger of each flavour: they share one signing secret."));
        }

        if (trigger.Flavour == WebhookFlavour.Generic)
        {
            if (trigger.Events.Count + trigger.Actions.Count + trigger.Branches.Count > 0)
            {
                issues.Add(new AutomationValidationIssue("webhook", "Event, action and branch filters belong to GitHub webhook triggers; a generic webhook starts a run on every valid delivery."));
            }

            return;
        }

        CheckList(trigger.Events, "events", static value => GitHubEvents.Contains(value, StringComparer.Ordinal), issues);
        CheckList(trigger.Actions, "actions", AutomationNames.IsFilterWord, issues);
        CheckList(trigger.Branches, "branches", AutomationNames.IsBranchText, issues);
    }

    private static void CheckList(IReadOnlyList<string> values, string what, Func<string, bool> accept, List<AutomationValidationIssue> issues)
    {
        if (values.Count > AutomationLimits.MaxListEntries
            || values.Any(value => !accept(value))
            || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            issues.Add(new AutomationValidationIssue("webhook", $"The webhook {what} list holds an unknown, malformed or repeated entry, or more than 16."));
        }
    }

    private static void ValidateTarget(UserAutomation automation, List<AutomationValidationIssue> issues, bool apiHost, ISessionIsolationLevel isolation)
    {
        var target = automation.Target;
        if (target.Kind == AutomationTargetKind.Session)
        {
            ValidateSession(target, issues, apiHost, isolation);
            return;
        }

        if (target.Kind == AutomationTargetKind.Local)
        {
            if (target.Repositories.Count > 0 || target.SecretRefs.Count > 0 || target.EnvironmentId is not null)
            {
                issues.Add(new AutomationValidationIssue("target", "A local target runs in the open workspace: it takes no repositories, secrets or environment."));
            }

            return;
        }

        if (target.Repositories.Count is 0 or > AutomationLimits.MaxRepositories)
        {
            issues.Add(new AutomationValidationIssue("repositories", "A cloud target needs between 1 and 16 repositories."));
        }

        foreach (var repository in target.Repositories)
        {
            if (!IsCloneUrl(repository.Address)
                || (repository.Branch is not null && !AutomationNames.IsBranchText(repository.Branch))
                || (repository.BaseBranch is not null && !AutomationNames.IsBranchText(repository.BaseBranch)))
            {
                issues.Add(new AutomationValidationIssue("repositories", "A repository needs an https URL with no credentials, query or fragment, and valid branch names."));
                break;
            }
        }

        if (target.SecretRefs.Count > AutomationLimits.MaxSecretRefs
            || target.SecretRefs.Any(name => !AutomationNames.IsSecretName(name))
            || target.SecretRefs.Distinct(StringComparer.Ordinal).Count() != target.SecretRefs.Count)
        {
            issues.Add(new AutomationValidationIssue("secrets", "Secret names are up to 32 distinct names of letters, digits and underscores that do not start with a digit."));
        }

        if ((target.EnvironmentId is not null && !AutomationNames.IsHint(target.EnvironmentId))
            || (target.Model is not null && !AutomationNames.IsHint(target.Model))
            || (target.Mode is not null && !AutomationNames.IsHint(target.Mode)))
        {
            issues.Add(new AutomationValidationIssue("target", "The environment, model and mode are at most 100 printable characters."));
        }
    }

    private static void ValidateSession(AutomationTargetSpec target, List<AutomationValidationIssue> issues, bool apiHost, ISessionIsolationLevel isolation)
    {
        if (apiHost)
        {
            issues.Add(new AutomationValidationIssue("target", "This API cannot run session automations. Use a cloud target, or run the automation from the desktop app."));
        }

        if (target.Repositories.Count > 0 || target.SecretRefs.Count > 0 || target.EnvironmentId is not null)
        {
            issues.Add(new AutomationValidationIssue("target", "A session target runs in a desktop session: it takes no repositories, secrets or environment."));
        }

        if (target.TrustedFolders.Count > AutomationLimits.MaxListEntries
            || target.TrustedFolders.Any(static folder => string.IsNullOrWhiteSpace(folder) || folder.Length > AutomationLimits.MaxEntryLength || folder.Any(char.IsControl)))
        {
            issues.Add(new AutomationValidationIssue("target", "Trusted folders are at most 16 paths, each at most 200 characters, with no control characters."));
        }

        if ((target.ProjectId is not null && !AutomationNames.IsHint(target.ProjectId))
            || (target.Model is not null && !AutomationNames.IsHint(target.Model))
            || (target.Mode is not null && !AutomationNames.IsHint(target.Mode)))
        {
            issues.Add(new AutomationValidationIssue("target", "The project, model and mode are at most 100 printable characters."));
        }

        var isolated = isolation.Level == SessionIsolationLevel.FilesystemIsolated;
        if (target.ApprovalMode == SessionApprovalMode.Skip && !isolated && !target.SkipAcknowledged)
        {
            issues.Add(new AutomationValidationIssue("target", "Skip was refused because the session is not filesystem-isolated."));
        }
    }

    private static bool IsCloneUrl(string? text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        && uri.UserInfo.Length == 0
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
        && uri.Host.Length > 0
        && uri.AbsolutePath.Length > 1;

    private static void ValidateTools(UserAutomation automation, List<AutomationValidationIssue> issues)
    {
        var tools = automation.AllowedTools;
        if (tools.Count > AutomationLimits.MaxListEntries
            || tools.Any(tool => !AutomationTools.Known.Contains(tool, StringComparer.Ordinal))
            || tools.Distinct(StringComparer.Ordinal).Count() != tools.Count)
        {
            issues.Add(new AutomationValidationIssue("tools", "Allowed tools must be distinct names from the known list: pull_request_create, pull_request_comment, pull_request_request_reviewers."));
        }
    }

    private static void ValidateLimits(UserAutomation automation, List<AutomationValidationIssue> issues)
    {
        if (automation.MaxConcurrentRuns is < 1 or > AutomationLimits.MaxConcurrentRunsCeiling
            || automation.MaxRunsPerHour is < 1 or > AutomationLimits.MaxRunsPerHourCeiling
            || automation.MaxRuntimeMinutes is < 1 or > AutomationLimits.MaxRuntimeMinutesCeiling)
        {
            issues.Add(new AutomationValidationIssue("limits", "Concurrent runs are 1 to 4, runs per hour 1 to 60 and the maximum runtime 1 to 360 minutes."));
        }
    }
}
