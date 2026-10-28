using Autonogrammer.Presentation.Automations;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Automations;

/// <summary>
/// One row of the hosted automations list: how triggers, target, next run and latest result read, every run status has its own words, and the
/// accessible names of the actions say which automation they act on.
/// </summary>
public sealed class HostedAutomationRowModelTests
{
    /// <summary>A schedule and a GitHub webhook read as two lines joined by a semicolon, with the GitHub events as words.</summary>
    [Fact]
    public void ScheduleAndGithubWebhookReadAsTwoLines()
    {
        var draft = AutomationFixtures.Draft() with
        {
            TimeZone = "Europe/Berlin",
            Triggers =
            [
                new AutomationTriggerDraft("cron", "0 9 * * 1-5", string.Empty, [], [], []),
                AutomationFixtures.Webhook("github", "push", "pull_request"),
            ],
        };

        var row = new HostedAutomationRowModel(AutomationFixtures.Entry(draft: draft));

        row.TriggerText.ShouldBe("Schedule: 0 9 * * 1-5 (Europe/Berlin); Webhook: GitHub push, pull request");
    }

    /// <summary>A generic webhook, a GitHub webhook without events and an automation without trigger each have their own words.</summary>
    [Fact]
    public void WebhookFlavoursAndNoTriggerHaveTheirOwnWords()
    {
        var generic = AutomationFixtures.Draft() with { Triggers = [AutomationFixtures.Webhook()] };
        var github = AutomationFixtures.Draft() with { Triggers = [AutomationFixtures.Webhook("github")] };
        var none = AutomationFixtures.Draft() with { Triggers = [] };

        new HostedAutomationRowModel(AutomationFixtures.Entry(draft: generic)).TriggerText.ShouldBe("Webhook");
        new HostedAutomationRowModel(AutomationFixtures.Entry(draft: github)).TriggerText.ShouldBe("Webhook: GitHub");
        new HostedAutomationRowModel(AutomationFixtures.Entry(draft: none)).TriggerText.ShouldBe(AutomationsCopy.NoTriggers);
    }

    /// <summary>A cloud automation names its first repository by owner and name, counts the others, and a local one says local session.</summary>
    [Fact]
    public void TargetNamesTheFirstRepositoryOrTheLocalSession()
    {
        var two = AutomationFixtures.Draft() with
        {
            Repositories =
            [
                new AutomationRepositoryDraft("https://github.com/owner/app.git", "work", string.Empty),
                new AutomationRepositoryDraft("https://github.com/owner/lib", string.Empty, string.Empty),
            ],
        };
        var none = AutomationFixtures.Draft() with { Repositories = [] };
        var local = AutomationFixtures.Draft() with { TargetKind = "local", Repositories = [] };

        new HostedAutomationRowModel(AutomationFixtures.Entry(draft: AutomationFixtures.Draft())).TargetText
            .ShouldBe("Cloud agent: owner/repo");
        new HostedAutomationRowModel(AutomationFixtures.Entry(draft: two)).TargetText
            .ShouldBe("Cloud agent: owner/app (+1 more)");
        new HostedAutomationRowModel(AutomationFixtures.Entry(draft: none)).TargetText.ShouldBe("Cloud agent");
        new HostedAutomationRowModel(AutomationFixtures.Entry(draft: local)).TargetText.ShouldBe("Local session");
    }

    /// <summary>The next run is shown in UTC whatever the offset it came with, and an unscheduled automation says so.</summary>
    [Fact]
    public void NextRunIsShownInUtc()
    {
        var scheduled = AutomationFixtures.Entry(nextRunAt: new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(1)));
        var unscheduled = AutomationFixtures.Entry(nextRunAt: null);

        new HostedAutomationRowModel(scheduled).NextRunText.ShouldBe("Next run: 2026-11-01 09:00 UTC");
        new HostedAutomationRowModel(unscheduled).NextRunText.ShouldBe("Next run: not scheduled");
    }

    /// <summary>An automation that has not run says so.</summary>
    [Fact]
    public void AutomationThatNeverRanSaysSo()
    {
        new HostedAutomationRowModel(AutomationFixtures.Entry()).LastResultText.ShouldBe("Last run: never");
    }

    /// <summary>Each finished status reads in its own words with the start time in UTC.</summary>
    [Theory]
    [InlineData(HostedRunStatus.Succeeded, "Last run: succeeded at 2026-10-28 09:00:05 UTC")]
    [InlineData(HostedRunStatus.Launched, "Last run: launched at 2026-10-28 09:00:05 UTC")]
    [InlineData(HostedRunStatus.Failed, "Last run: failed at 2026-10-28 09:00:05 UTC")]
    [InlineData(HostedRunStatus.TimedOut, "Last run: timed out at 2026-10-28 09:00:05 UTC")]
    [InlineData(HostedRunStatus.Cancelled, "Last run: cancelled at 2026-10-28 09:00:05 UTC")]
    [InlineData(HostedRunStatus.Skipped, "Last run: skipped at 2026-10-28 09:00:05 UTC")]
    [InlineData(HostedRunStatus.Running, "Last run: running since 2026-10-28 09:00:05 UTC")]
    public void EveryStatusReadsInItsOwnWords(HostedRunStatus status, string expected)
    {
        var entry = AutomationFixtures.Entry(lastRun: AutomationFixtures.Run("run-1", status));

        new HostedAutomationRowModel(entry).LastResultText.ShouldBe(expected);
    }

    /// <summary>No status is left without words: a status added later fails here instead of reading as unknown.</summary>
    [Fact]
    public void NoStatusReadsAsUnknown()
    {
        foreach (var status in Enum.GetValues<HostedRunStatus>())
        {
            var entry = AutomationFixtures.Entry(lastRun: AutomationFixtures.Run("run-1", status));

            new HostedAutomationRowModel(entry).LastResultText.ShouldNotContain("unknown", Case.Sensitive);
            new HostedRunRowModel(AutomationFixtures.Run("run-1", status)).StatusText.ShouldNotContain("Unknown", Case.Sensitive);
        }
    }

    /// <summary>The reason follows in brackets: a run's error wins over its summary, and a summary shows when there is no error.</summary>
    [Fact]
    public void ReasonFollowsTheResult()
    {
        var failed = AutomationFixtures.Run("run-1", HostedRunStatus.Failed, summary: "Cloning", error: "clone failed");
        var succeeded = AutomationFixtures.Run("run-2", HostedRunStatus.Succeeded, summary: "Labelled 3 issues");

        new HostedAutomationRowModel(AutomationFixtures.Entry(lastRun: failed)).LastResultText
            .ShouldBe("Last run: failed at 2026-10-28 09:00:05 UTC (clone failed)");
        new HostedAutomationRowModel(AutomationFixtures.Entry(lastRun: succeeded)).LastResultText
            .ShouldBe("Last run: succeeded at 2026-10-28 09:00:05 UTC (Labelled 3 issues)");
    }

    /// <summary>The toggle names the change it makes, and every action's accessible name carries the automation name.</summary>
    [Fact]
    public void ActionNamesCarryTheAutomationName()
    {
        var on = new HostedAutomationRowModel(AutomationFixtures.Entry(draft: AutomationFixtures.Draft() with { Name = "Weekly digest" }));
        var off = new HostedAutomationRowModel(AutomationFixtures.Entry(draft: AutomationFixtures.Draft() with { Name = "Weekly digest", Enabled = false }));

        on.IsEnabled.ShouldBeTrue();
        on.ToggleText.ShouldBe("Turn off");
        on.EnabledText.ShouldBe("Enabled");
        off.IsEnabled.ShouldBeFalse();
        off.ToggleText.ShouldBe("Turn on");
        off.EnabledText.ShouldBe("Disabled");

        foreach (var name in new[] { on.ToggleName, on.RunNowName, on.EditName, on.DeleteName, on.AccessibleName, off.ToggleName })
        {
            name.ShouldContain("Weekly digest", Case.Sensitive);
        }

        on.ToggleName.ShouldNotBe(off.ToggleName, StringComparer.Ordinal);
        new[] { on.RunNowName, on.EditName, on.DeleteName, on.ToggleName }.Distinct(StringComparer.Ordinal).Count().ShouldBe(4);
    }

    /// <summary>The webhook path shows labelled when there is one and is empty otherwise; a row never holds a secret because an entry has none.</summary>
    [Fact]
    public void WebhookPathShowsLabelledOnlyWhenThereIsOne()
    {
        var withHook = new HostedAutomationRowModel(
            AutomationFixtures.Entry(draft: AutomationFixtures.Draft() with { Triggers = [AutomationFixtures.Webhook()] }, webhookPath: "/webhooks/auto-1", hasSecret: true));
        var without = new HostedAutomationRowModel(AutomationFixtures.Entry());

        withHook.WebhookPath.ShouldBe("/webhooks/auto-1");
        withHook.WebhookText.ShouldBe("Webhook path: /webhooks/auto-1");
        without.WebhookPath.ShouldBeEmpty();
        without.WebhookText.ShouldBeEmpty();
    }
}
