using Autonogrammer.Contracts.Automations;
using Autonogrammer.Harness.Automations.Hosted;

namespace Autonogrammer.Api.Automations;

/// <summary>
/// Converts between the wire shapes of <c>Autonogrammer.Contracts.Automations</c> and the automation model. The conversion never carries a webhook
/// secret: <see cref="AutomationInfo"/> says only whether one is stored.
/// </summary>
internal static class AutomationMapper
{
    /// <summary>The path prefix of the webhook receiver.</summary>
    public const string WebhookPathPrefix = "/hooks/automations/";

    /// <summary>Reads a save request into a draft. Unknown words become issues, never exceptions.</summary>
    /// <param name="request">The request.</param>
    /// <param name="issues">The problems found in the words (enum-like strings) of the request.</param>
    /// <returns>The draft; its remaining rules are checked by <see cref="AutomationValidator"/>.</returns>
    public static AutomationDraft ToDraft(SaveAutomationRequest request, List<AutomationValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(issues);
        var triggers = new List<AutomationTriggerSpec>();
        foreach (var trigger in request.Triggers ?? [])
        {
            if (trigger is null)
            {
                issues.Add(new AutomationValidationIssue("triggers", "A trigger is empty."));
                continue;
            }

            var spec = ToTrigger(trigger, issues);
            if (spec is not null)
            {
                triggers.Add(spec);
            }
        }

        var target = request.Target ?? new AutomationTargetInfo();
        var targetKind = ParseTargetKind(target.Kind, issues);
        return new AutomationDraft
        {
            Name = request.Name ?? string.Empty,
            Prompt = request.Prompt ?? string.Empty,
            Enabled = request.Enabled ?? false,
            Triggers = triggers,
            Target = new AutomationTargetSpec
            {
                Kind = targetKind,
                Repositories = (target.Repositories ?? []).Where(static repository => repository is not null)
                    .Select(static repository => new AutomationRepository(repository.Address ?? string.Empty, repository.Branch, repository.BaseBranch)).ToArray(),
                SecretRefs = target.SecretRefs ?? [],
                EnvironmentId = target.EnvironmentId,
                Model = target.Model,
                Mode = target.Mode,
            },
            AllowedTools = request.AllowedTools,
            MaxConcurrentRuns = request.MaxConcurrentRuns ?? AutomationLimits.DefaultMaxConcurrentRuns,
            MaxRunsPerHour = request.MaxRunsPerHour ?? AutomationLimits.DefaultMaxRunsPerHour,
            TimeZone = request.TimeZone ?? "UTC",
            MaxRuntimeMinutes = request.MaxRuntimeMinutes ?? AutomationLimits.DefaultMaxRuntimeMinutes,
        };
    }

    /// <summary>Builds the wire shape of an automation.</summary>
    /// <param name="automation">The automation.</param>
    /// <param name="lastRun">Its most recent run, or null.</param>
    /// <param name="hasSecret">Whether a webhook secret is stored.</param>
    /// <param name="now">The current time, for the next run.</param>
    /// <returns>The shape.</returns>
    public static AutomationInfo ToInfo(UserAutomation automation, AutomationRunEntry? lastRun, bool hasSecret, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(automation);
        return new AutomationInfo
        {
            Id = automation.Id,
            Name = automation.Name,
            Prompt = automation.Prompt,
            Enabled = automation.Enabled,
            Triggers = automation.Triggers.Select(ToTriggerInfo).ToArray(),
            Target = new AutomationTargetInfo
            {
                Kind = automation.Target.Kind == AutomationTargetKind.Local ? "local" : "cloud",
                Repositories = automation.Target.Repositories.Select(static repository => new AutomationRepositoryInfo { Address = repository.Address, Branch = repository.Branch, BaseBranch = repository.BaseBranch }).ToArray(),
                SecretRefs = automation.Target.SecretRefs,
                EnvironmentId = automation.Target.EnvironmentId,
                Model = automation.Target.Model,
                Mode = automation.Target.Mode,
            },
            AllowedTools = automation.AllowedTools,
            MaxConcurrentRuns = automation.MaxConcurrentRuns,
            MaxRunsPerHour = automation.MaxRunsPerHour,
            TimeZone = automation.TimeZone,
            MaxRuntimeMinutes = automation.MaxRuntimeMinutes,
            CreatedAt = automation.CreatedAt,
            UpdatedAt = automation.UpdatedAt,
            NextRunAt = automation.Enabled ? AutomationNextRun.Next(automation, now) : null,
            LastRun = lastRun is null ? null : ToRunInfo(lastRun),
            WebhookPath = automation.HasWebhook ? WebhookPathPrefix + automation.Id : null,
            HasWebhookSecret = hasSecret,
        };
    }

    /// <summary>Builds the wire shape of a run.</summary>
    /// <param name="entry">The history entry.</param>
    /// <returns>The shape.</returns>
    public static AutomationRunInfo ToRunInfo(AutomationRunEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new AutomationRunInfo
        {
            RunId = entry.RunId,
            Trigger = entry.Source switch
            {
                AutomationRunSource.Manual => "manual",
                AutomationRunSource.Cron => "cron",
                _ => "webhook",
            },
            Summary = entry.Summary,
            Status = StatusWord(entry.Status),
            StartedAt = entry.StartedAt,
            FinishedAt = entry.FinishedAt,
            DurationSeconds = entry.Duration?.TotalSeconds,
            Error = entry.Error,
            ExternalId = entry.ExternalId,
        };
    }

    /// <summary>Returns the wire word of a run status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The word.</returns>
    public static string StatusWord(AutomationRunStatus status) => status switch
    {
        AutomationRunStatus.Running => "running",
        AutomationRunStatus.Succeeded => "succeeded",
        AutomationRunStatus.Launched => "launched",
        AutomationRunStatus.Failed => "failed",
        AutomationRunStatus.TimedOut => "timed_out",
        AutomationRunStatus.Cancelled => "cancelled",
        _ => "skipped",
    };

    private static AutomationTriggerInfo ToTriggerInfo(AutomationTriggerSpec trigger) =>
        trigger.Kind == AutomationTriggerKind.Cron
            ? new AutomationTriggerInfo { Kind = "cron", Cron = trigger.Cron }
            : new AutomationTriggerInfo
            {
                Kind = "webhook",
                Flavour = trigger.Flavour == WebhookFlavour.GitHub ? "github" : "generic",
                Events = trigger.Events,
                Actions = trigger.Actions,
                Branches = trigger.Branches,
            };

    private static AutomationTriggerSpec? ToTrigger(AutomationTriggerInfo trigger, List<AutomationValidationIssue> issues)
    {
        if (string.Equals(trigger.Kind, "cron", StringComparison.Ordinal))
        {
            return new AutomationTriggerSpec { Kind = AutomationTriggerKind.Cron, Cron = trigger.Cron };
        }

        if (!string.Equals(trigger.Kind, "webhook", StringComparison.Ordinal))
        {
            issues.Add(new AutomationValidationIssue("triggers", "A trigger kind is not known. Use cron or webhook."));
            return null;
        }

        WebhookFlavour flavour;
        if (trigger.Flavour is null || string.Equals(trigger.Flavour, "generic", StringComparison.Ordinal))
        {
            flavour = WebhookFlavour.Generic;
        }
        else if (string.Equals(trigger.Flavour, "github", StringComparison.Ordinal))
        {
            flavour = WebhookFlavour.GitHub;
        }
        else
        {
            issues.Add(new AutomationValidationIssue("webhook", "A webhook flavour is not known. Use generic or github."));
            return null;
        }

        return new AutomationTriggerSpec
        {
            Kind = AutomationTriggerKind.Webhook,
            Flavour = flavour,
            Events = trigger.Events ?? [],
            Actions = trigger.Actions ?? [],
            Branches = trigger.Branches ?? [],
        };
    }

    private static AutomationTargetKind ParseTargetKind(string? kind, List<AutomationValidationIssue> issues)
    {
        if (kind is null || string.Equals(kind, "cloud", StringComparison.Ordinal))
        {
            return AutomationTargetKind.Cloud;
        }

        if (string.Equals(kind, "local", StringComparison.Ordinal))
        {
            return AutomationTargetKind.Local;
        }

        issues.Add(new AutomationValidationIssue("target", "The target kind is not known. Use cloud or local."));
        return AutomationTargetKind.Cloud;
    }
}
