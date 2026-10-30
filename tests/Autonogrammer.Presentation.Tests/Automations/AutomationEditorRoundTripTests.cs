using Autonogrammer.Presentation.Automations;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Automations;

/// <summary>Opening an existing automation in the editor and reading the draft back changes nothing: every field survives the text form.</summary>
public sealed class AutomationEditorRoundTripTests
{
    /// <summary>A cloud automation with a schedule, a GitHub webhook, repositories with branches, secret names and every limit survives intact.</summary>
    [Fact]
    public void FullCloudAutomationSurvivesTheRoundTrip()
    {
        var original = new AutomationDraft(
            Name: "Review pushes",
            Prompt: "Review every push.\nBe brief.\n",
            Enabled: false,
            TimeZone: "Europe/Berlin",
            Triggers:
            [
                new AutomationTriggerDraft("cron", "0 9 * * 1-5", string.Empty, [], [], []),
                new AutomationTriggerDraft("webhook", string.Empty, "github", ["push", "pull_request"], ["opened", "synchronize"], ["main", "release/*"]),
            ],
            TargetKind: "cloud",
            Repositories:
            [
                new AutomationRepositoryDraft("https://github.com/owner/app", "work", "main"),
                new AutomationRepositoryDraft("https://github.com/owner/lib", string.Empty, "develop"),
                new AutomationRepositoryDraft("https://github.com/owner/docs", "wip", string.Empty),
                new AutomationRepositoryDraft("https://github.com/owner/site", string.Empty, string.Empty),
            ],
            SecretRefs: ["API_KEY", "DEPLOY_TOKEN"],
            EnvironmentId: "env-prod",
            Model: "model-x",
            Mode: "plan",
            AllowedTools: [AutomationEditorModel.PullRequestCreateTool, AutomationEditorModel.PullRequestRequestReviewersTool],
            MaxConcurrentRuns: 3,
            MaxRunsPerHour: 40,
            MaxRuntimeMinutes: 120);
        var model = new AutomationEditorModel(new FakeAutomationsManager(), AutomationFixtures.Entry(draft: original));

        var draft = model.ToDraft();

        AssertSame(draft, original);
        model.CanSave.ShouldBeTrue();
        model.Errors.ShouldBeEmpty();
    }

    /// <summary>A local automation with only a generic webhook survives, and carries no repositories or secret names.</summary>
    [Fact]
    public void LocalWebhookAutomationSurvivesTheRoundTrip()
    {
        var original = AutomationFixtures.Draft() with
        {
            TargetKind = "local",
            Repositories = [],
            Triggers = [AutomationFixtures.Webhook()],
            AllowedTools = [],
            MaxRunsPerHour = 1,
            MaxRuntimeMinutes = 360,
        };
        var model = new AutomationEditorModel(new FakeAutomationsManager(), AutomationFixtures.Entry(draft: original, webhookPath: "/webhooks/auto-1", hasSecret: true));

        AssertSame(model.ToDraft(), original);
        model.WebhookPath.ShouldBe("/webhooks/auto-1");
        model.HasWebhookSecret.ShouldBeTrue();
        model.IsEdit.ShouldBeTrue();
        model.WebhookEnabled.ShouldBeTrue();
        model.CronText.ShouldBeEmpty();
    }

    /// <summary>What the form has no field for is kept: further triggers and pull request actions it does not know come back unchanged.</summary>
    [Fact]
    public void UnknownToolsAndFurtherTriggersAreKept()
    {
        var original = AutomationFixtures.Draft() with
        {
            Triggers =
            [
                new AutomationTriggerDraft("cron", "0 9 * * 1-5", string.Empty, [], [], []),
                new AutomationTriggerDraft("cron", "0 18 * * 1-5", string.Empty, [], [], []),
            ],
            AllowedTools = ["pull_request_create", "issue_comment"],
        };
        var model = new AutomationEditorModel(new FakeAutomationsManager(), AutomationFixtures.Entry(draft: original));

        var draft = model.ToDraft();

        model.CronText.ShouldBe("0 9 * * 1-5");
        model.AllowPullRequestCreate.ShouldBeTrue();
        AssertSame(draft, original);
    }

    /// <summary>The form shows the fields of the entry it was opened on and starts without messages or a revealed secret.</summary>
    [Fact]
    public void FormShowsTheEntryItWasOpenedOn()
    {
        var entry = AutomationFixtures.Entry(draft: AutomationFixtures.Draft() with
        {
            Triggers =
            [
                new AutomationTriggerDraft("cron", "0 9 * * 1-5", string.Empty, [], [], []),
                AutomationFixtures.Webhook("github", "push"),
            ],
            Repositories =
            [
                new AutomationRepositoryDraft("https://github.com/owner/app", "work", "main"),
                new AutomationRepositoryDraft("https://github.com/owner/lib", string.Empty, "develop"),
            ],
            SecretRefs = ["API_KEY", "TOKEN"],
            MaxConcurrentRuns = 2,
        });

        var model = new AutomationEditorModel(new FakeAutomationsManager(), entry);

        model.Name.ShouldBe("Nightly triage");
        model.CronText.ShouldBe("0 9 * * 1-5");
        model.WebhookEnabled.ShouldBeTrue();
        model.WebhookFlavour.ShouldBe("github");
        model.WebhookEventsText.ShouldBe("push");
        model.TargetKind.ShouldBe("cloud");
        model.RepositoriesText.ShouldBe("https://github.com/owner/app work main\nhttps://github.com/owner/lib - develop");
        model.SecretRefsText.ShouldBe("API_KEY, TOKEN");
        model.MaxConcurrentRunsText.ShouldBe("2");
        model.Errors.ShouldBeEmpty();
        model.RevealedSecret.ShouldBeEmpty();
        model.SavedEntry.ShouldBeNull();
    }

    /// <summary>An entry whose target this application cannot run opens with the target message, so the user sees why Save is unavailable.</summary>
    [Fact]
    public void EntryWithAnUnsupportedTargetCannotBeSavedUntilTheTargetChanges()
    {
        var entry = AutomationFixtures.Entry(draft: AutomationFixtures.Draft() with { TargetKind = "local", Repositories = [] });
        var model = new AutomationEditorModel(new FakeAutomationsManager { SupportedTargetKinds = ["cloud"] }, entry);

        model.CanSave.ShouldBeFalse();
        model.WhyDisabled.ShouldContain("Target", Case.Sensitive);

        model.TargetKind = "cloud";
        model.RepositoriesText = "https://github.com/owner/repo";

        model.CanSave.ShouldBeTrue();
    }

    private static void AssertSame(AutomationDraft actual, AutomationDraft expected)
    {
        actual.Name.ShouldBe(expected.Name);
        actual.Prompt.ShouldBe(expected.Prompt);
        actual.Enabled.ShouldBe(expected.Enabled);
        actual.TimeZone.ShouldBe(expected.TimeZone);
        actual.TargetKind.ShouldBe(expected.TargetKind);
        actual.EnvironmentId.ShouldBe(expected.EnvironmentId);
        actual.Model.ShouldBe(expected.Model);
        actual.Mode.ShouldBe(expected.Mode);
        actual.MaxConcurrentRuns.ShouldBe(expected.MaxConcurrentRuns);
        actual.MaxRunsPerHour.ShouldBe(expected.MaxRunsPerHour);
        actual.MaxRuntimeMinutes.ShouldBe(expected.MaxRuntimeMinutes);
        actual.SecretRefs.ToArray().ShouldBe(expected.SecretRefs.ToArray());
        actual.AllowedTools.ToArray().ShouldBe(expected.AllowedTools.ToArray());
        actual.Repositories.ToArray().ShouldBe(expected.Repositories.ToArray());

        actual.Triggers.Count.ShouldBe(expected.Triggers.Count);
        for (var index = 0; index < expected.Triggers.Count; index++)
        {
            var left = actual.Triggers[index];
            var right = expected.Triggers[index];
            left.Kind.ShouldBe(right.Kind);
            left.Cron.ShouldBe(right.Cron);
            left.Flavour.ShouldBe(right.Flavour);
            left.Events.ToArray().ShouldBe(right.Events.ToArray());
            left.Actions.ToArray().ShouldBe(right.Actions.ToArray());
            left.Branches.ToArray().ShouldBe(right.Branches.ToArray());
        }
    }
}
