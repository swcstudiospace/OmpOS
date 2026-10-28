using System.Globalization;
using OmpOS.Presentation.Automations;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Automations;

/// <summary>
/// The editor's client-side rules: what each field accepts at its boundaries, which message shows and under which field, that a message
/// never repeats what was typed, and that nothing reaches the manager before every rule passes.
/// </summary>
public sealed class AutomationEditorValidationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A new form starts with the documented defaults, shows no message yet, and says which fields keep Save unavailable.</summary>
    [Fact]
    public void NewFormStartsWithDefaultsAndNoMessages()
    {
        var model = new AutomationEditorModel(new FakeAutomationsManager(), null);

        model.IsEdit.ShouldBeFalse();
        model.TimeZone.ShouldBe("UTC", Case.Sensitive);
        model.Enabled.ShouldBeTrue();
        model.AllowPullRequestCreate.ShouldBeTrue();
        model.AllowPullRequestComment.ShouldBeFalse();
        model.AllowRequestReviewers.ShouldBeFalse();
        model.MaxConcurrentRunsText.ShouldBe("1", Case.Sensitive);
        model.MaxRunsPerHourText.ShouldBe("12", Case.Sensitive);
        model.MaxRuntimeMinutesText.ShouldBe("30", Case.Sensitive);
        model.TargetKind.ShouldBe("local", Case.Sensitive);
        model.SupportedTargetKinds.ShouldBe(["local", "cloud"]);
        model.Errors.ShouldBeEmpty();
        model.CanSave.ShouldBeFalse();
        model.WhyDisabled.ShouldBe(AutomationsCopy.SaveBlockedPrefix + "Name, Prompt, Triggers.", Case.Sensitive);
        model.RevealedSecret.ShouldBeEmpty();
        model.SavedEntry.ShouldBeNull();
    }

    /// <summary>A form with a name, a prompt, a schedule and a local target can be saved, and then says nothing about why not.</summary>
    [Fact]
    public void CompleteLocalFormCanBeSaved()
    {
        var model = ValidLocal(new FakeAutomationsManager());

        model.CanSave.ShouldBeTrue();
        model.WhyDisabled.ShouldBeEmpty();
        model.Errors.ShouldBeEmpty();
    }

    /// <summary>The default target is the first supported kind, so a web head that supports only cloud starts on cloud.</summary>
    [Fact]
    public void DefaultTargetIsTheFirstSupportedKind()
    {
        var cloudOnly = new AutomationEditorModel(new FakeAutomationsManager { SupportedTargetKinds = ["cloud"] }, null);

        cloudOnly.TargetKind.ShouldBe("cloud", Case.Sensitive);
        cloudOnly.IsCloudTarget.ShouldBeTrue();
    }

    /// <summary>A name of 100 characters is accepted and one of 101 is refused; surrounding spaces do not count; blank is refused.</summary>
    [Fact]
    public void NameBoundaryIs100Characters()
    {
        var model = ValidLocal(new FakeAutomationsManager());

        model.Name = new string('a', 100);
        model.Errors.ContainsKey("name").ShouldBeFalse();
        model.CanSave.ShouldBeTrue();

        model.Name = new string('a', 101);
        model.Errors["name"].ShouldBe(AutomationsCopy.NameTooLong, Case.Sensitive);
        model.CanSave.ShouldBeFalse();

        model.Name = " " + new string('a', 100) + " ";
        model.Errors.ContainsKey("name").ShouldBeFalse();

        model.Name = "   ";
        model.Errors["name"].ShouldBe(AutomationsCopy.NameRequired, Case.Sensitive);
    }

    /// <summary>A prompt of 20000 characters is accepted and one of 20001 is refused; blank is refused.</summary>
    [Fact]
    public void PromptBoundaryIs20000Characters()
    {
        var model = ValidLocal(new FakeAutomationsManager());

        model.Prompt = new string('p', 20000);
        model.Errors.ContainsKey("prompt").ShouldBeFalse();

        model.Prompt = new string('p', 20001);
        model.Errors["prompt"].ShouldBe(AutomationsCopy.PromptTooLong, Case.Sensitive);

        model.Prompt = " \n ";
        model.Errors["prompt"].ShouldBe(AutomationsCopy.PromptRequired, Case.Sensitive);
        model.CanSave.ShouldBeFalse();
    }

    /// <summary>Without a schedule and without a webhook there is no trigger; either one is enough.</summary>
    [Fact]
    public void AtLeastOneTriggerIsRequired()
    {
        var model = ValidLocal(new FakeAutomationsManager());

        model.CronText = string.Empty;
        model.Errors["triggers"].ShouldBe(AutomationsCopy.TriggersRequired, Case.Sensitive);
        model.CanSave.ShouldBeFalse();

        model.WebhookEnabled = true;
        model.Errors.ContainsKey("triggers").ShouldBeFalse();
        model.CanSave.ShouldBeTrue();

        model.WebhookEnabled = false;
        model.CronText = "*/5 * * * *";
        model.Errors.ContainsKey("triggers").ShouldBeFalse();
    }

    /// <summary>The webhook type must be generic or GitHub.</summary>
    [Fact]
    public void WebhookTypeMustBeKnown()
    {
        var model = ValidLocal(new FakeAutomationsManager());
        model.WebhookEnabled = true;

        model.WebhookFlavour = "gitlab";
        model.Errors["webhook"].ShouldBe(AutomationsCopy.WebhookFlavourInvalid, Case.Sensitive);

        model.WebhookFlavour = "github";
        model.Errors.ContainsKey("webhook").ShouldBeFalse();

        model.WebhookFlavour = "generic";
        model.Errors.ContainsKey("webhook").ShouldBeFalse();
    }

    /// <summary>GitHub filters accept names and globs, refuse spaces, and are ignored by a generic webhook.</summary>
    [Fact]
    public void GithubFiltersAreCheckedOnlyForAGithubWebhook()
    {
        var model = ValidLocal(new FakeAutomationsManager());
        model.WebhookEnabled = true;
        model.WebhookFlavour = "github";

        model.WebhookEventsText = "push, pull_request";
        model.WebhookActionsText = "opened,synchronize";
        model.WebhookBranchesText = "main, release/*";
        model.Errors.ContainsKey("webhook").ShouldBeFalse();

        model.WebhookEventsText = "push, bad event";
        model.Errors["webhook"].ShouldBe(AutomationsCopy.WebhookFilterInvalid, Case.Sensitive);
        model.Errors["webhook"].ShouldNotContain("bad event", Case.Sensitive);

        model.WebhookEventsText = "push";
        model.WebhookBranchesText = "my branch";
        model.Errors.ContainsKey("webhook").ShouldBeTrue();

        model.WebhookFlavour = "generic";
        model.Errors.ContainsKey("webhook").ShouldBeFalse();
        model.CanSave.ShouldBeTrue();
    }

    /// <summary>A target the application cannot run is refused with its own message.</summary>
    [Fact]
    public void UnsupportedTargetIsRefused()
    {
        var manager = new FakeAutomationsManager { SupportedTargetKinds = ["cloud"] };
        var model = new AutomationEditorModel(manager, null) { Name = "n", Prompt = "p", CronText = "0 * * * *", RepositoriesText = Repository(1) };
        model.CanSave.ShouldBeTrue();

        model.TargetKind = "local";

        model.Errors["target"].ShouldBe(AutomationsCopy.TargetUnsupported, Case.Sensitive);
        model.CanSave.ShouldBeFalse();

        model.TargetKind = "mainframe";
        model.Errors.ContainsKey("target").ShouldBeTrue();
    }

    /// <summary>A cloud target needs a repository; a local target does not and ignores whatever is typed in the field.</summary>
    [Fact]
    public void CloudTargetNeedsARepositoryAndLocalIgnoresThem()
    {
        var model = ValidLocal(new FakeAutomationsManager());

        model.TargetKind = "cloud";
        model.Errors["repositories"].ShouldBe(AutomationsCopy.RepositoriesRequired, Case.Sensitive);
        model.CanSave.ShouldBeFalse();

        model.RepositoriesText = Repository(1);
        model.Errors.ContainsKey("repositories").ShouldBeFalse();
        model.CanSave.ShouldBeTrue();

        model.RepositoriesText = "not an address";
        model.TargetKind = "local";
        model.Errors.ContainsKey("repositories").ShouldBeFalse();
        model.CanSave.ShouldBeTrue();
        model.ToDraft().Repositories.ShouldBeEmpty();
    }

    /// <summary>Sixteen repositories are accepted and seventeen are refused; blank lines do not count.</summary>
    [Fact]
    public void RepositoryBoundaryIs16()
    {
        var model = ValidCloud(new FakeAutomationsManager());

        model.RepositoriesText = Repositories(16);
        model.Errors.ContainsKey("repositories").ShouldBeFalse();
        model.CanSave.ShouldBeTrue();

        model.RepositoriesText = Repositories(17);
        model.Errors["repositories"].ShouldBe(AutomationsCopy.RepositoriesTooMany, Case.Sensitive);
        model.CanSave.ShouldBeFalse();

        model.RepositoriesText = Repositories(16) + "\n\n  \n";
        model.Errors.ContainsKey("repositories").ShouldBeFalse();
        model.ToDraft().Repositories.Count.ShouldBe(16);
    }

    /// <summary>
    /// A repository must be an https address without credentials, and may carry a branch and a base branch; the message never repeats the
    /// entry, so a credential typed by mistake is not echoed.
    /// </summary>
    [Theory]
    [InlineData("https://github.com/owner/repo", true)]
    [InlineData("HTTPS://github.com/owner/repo", true)]
    [InlineData("https://github.com/owner/repo work", true)]
    [InlineData("https://github.com/owner/repo work main", true)]
    [InlineData("https://github.com/owner/repo - main", true)]
    [InlineData("https://github.com/owner/repo work main extra", false)]
    [InlineData("http://github.com/owner/repo", false)]
    [InlineData("git@github.com:owner/repo.git", false)]
    [InlineData("ssh://github.com/owner/repo", false)]
    [InlineData("https://", false)]
    [InlineData("https:///owner/repo", false)]
    [InlineData("https://user:hunter2@github.com/owner/repo", false)]
    [InlineData("https://hunter2@github.com/owner/repo", false)]
    [InlineData("github.com/owner/repo", false)]
    public void RepositoryEntryIsCheckedAndNeverEchoed(string entry, bool valid)
    {
        var model = ValidCloud(new FakeAutomationsManager());

        model.RepositoriesText = entry;

        model.Errors.ContainsKey("repositories").ShouldBe(!valid);
        model.CanSave.ShouldBe(valid);
        if (!valid)
        {
            model.Errors["repositories"].ShouldBe(AutomationsCopy.RepositoryInvalid, Case.Sensitive);
            model.Errors["repositories"].ShouldNotContain("hunter2", Case.Sensitive);
        }
    }

    /// <summary>Secret names are names, not values: letters, digits and underscores, not starting with a digit, up to 64 characters.</summary>
    [Theory]
    [InlineData("API_KEY", true)]
    [InlineData("API_KEY, _private1,Token", true)]
    [InlineData("", true)]
    [InlineData(",,", true)]
    [InlineData("1ABC", false)]
    [InlineData("has-dash", false)]
    [InlineData("has space", false)]
    [InlineData("naïve", false)]
    [InlineData("OK, bad.name", false)]
    public void SecretNamesAreChecked(string entry, bool valid)
    {
        var model = ValidCloud(new FakeAutomationsManager());

        model.SecretRefsText = entry;

        model.Errors.ContainsKey("secrets").ShouldBe(!valid);
        model.CanSave.ShouldBe(valid);
        if (!valid)
        {
            model.Errors["secrets"].ShouldBe(AutomationsCopy.SecretNameInvalid, Case.Sensitive);
        }
    }

    /// <summary>A secret name of 64 characters is accepted and one of 65 is refused; a local target does not check them.</summary>
    [Fact]
    public void SecretNameBoundaryIs64Characters()
    {
        var model = ValidCloud(new FakeAutomationsManager());

        model.SecretRefsText = new string('S', 64);
        model.Errors.ContainsKey("secrets").ShouldBeFalse();

        model.SecretRefsText = new string('S', 65);
        model.Errors.ContainsKey("secrets").ShouldBeTrue();

        model.TargetKind = "local";
        model.Errors.ContainsKey("secrets").ShouldBeFalse();
        model.ToDraft().SecretRefs.ShouldBeEmpty();
    }

    /// <summary>Each limit is a whole number inside its range, and the boundaries themselves are accepted.</summary>
    [Theory]
    [InlineData("concurrent", "0", false)]
    [InlineData("concurrent", "1", true)]
    [InlineData("concurrent", "4", true)]
    [InlineData("concurrent", "5", false)]
    [InlineData("perhour", "0", false)]
    [InlineData("perhour", "1", true)]
    [InlineData("perhour", "60", true)]
    [InlineData("perhour", "61", false)]
    [InlineData("runtime", "0", false)]
    [InlineData("runtime", "1", true)]
    [InlineData("runtime", "360", true)]
    [InlineData("runtime", "361", false)]
    [InlineData("runtime", "", false)]
    [InlineData("runtime", "abc", false)]
    [InlineData("runtime", "-5", false)]
    [InlineData("runtime", "1.5", false)]
    [InlineData("runtime", "+5", false)]
    [InlineData("runtime", " 45 ", true)]
    [InlineData("runtime", "99999999999", false)]
    public void LimitsAreWholeNumbersInRange(string which, string text, bool valid)
    {
        var model = ValidLocal(new FakeAutomationsManager());

        switch (which)
        {
            case "concurrent":
                model.MaxConcurrentRunsText = text;
                break;
            case "perhour":
                model.MaxRunsPerHourText = text;
                break;
            default:
                model.MaxRuntimeMinutesText = text;
                break;
        }

        model.Errors.ContainsKey("limits").ShouldBe(!valid);
        model.CanSave.ShouldBe(valid);
    }

    /// <summary>A bad limit names which limit is wrong and does not repeat what was typed; two bad limits give two sentences.</summary>
    [Fact]
    public void LimitMessagesNameTheLimitAndNeverEchoTheEntry()
    {
        var model = ValidLocal(new FakeAutomationsManager());

        model.MaxConcurrentRunsText = "nine";
        model.MaxRuntimeMinutesText = "forever";

        var message = model.Errors["limits"];
        message.ShouldContain(AutomationsCopy.ConcurrentRunsInvalid, Case.Sensitive);
        message.ShouldContain(AutomationsCopy.RuntimeInvalid, Case.Sensitive);
        message.ShouldNotContain(AutomationsCopy.RunsPerHourInvalid, Case.Sensitive);
        message.ShouldNotContain("nine", Case.Sensitive);
        message.ShouldNotContain("forever", Case.Sensitive);
    }

    /// <summary>Save keeps naming the missing fields in form order, whatever the order they were broken in.</summary>
    [Fact]
    public void WhyDisabledNamesTheBlockingFieldsInFormOrder()
    {
        var model = ValidCloud(new FakeAutomationsManager());

        model.MaxRuntimeMinutesText = "0";
        model.RepositoriesText = string.Empty;
        model.Name = " ";

        model.WhyDisabled.ShouldBe(AutomationsCopy.SaveBlockedPrefix + "Name, Repositories, Limits.", Case.Sensitive);
    }

    /// <summary>
    /// Saving a blank form shows every message at once and makes no manager call; a message appears for a field the user did not touch only
    /// after that attempt.
    /// </summary>
    [Fact]
    public async Task SavingABlankFormShowsEveryMessageAndCallsNothing()
    {
        var manager = new FakeAutomationsManager();
        var model = new AutomationEditorModel(manager, null);
        model.Errors.ShouldBeEmpty();

        await model.SaveAsync(Token).ConfigureAwait(true);

        model.Errors.Keys.OrderBy(key => key, StringComparer.Ordinal).ShouldBe(["name", "prompt", "triggers"]);
        manager.Calls.ShouldBeEmpty();
        model.IsSaving.ShouldBeFalse();
        model.SaveFailure.ShouldBeEmpty();
        model.SavedEntry.ShouldBeNull();
    }

    /// <summary>The draft built from a valid cloud form carries what was typed, trimmed, with lists split and limits parsed.</summary>
    [Fact]
    public void DraftCarriesTheTypedEntries()
    {
        var model = new AutomationEditorModel(new FakeAutomationsManager(), null)
        {
            Name = "  Nightly triage ",
            Prompt = "Triage new issues.\n",
            TimeZone = "Europe/Berlin",
            CronText = " 0 9 * * 1-5 ",
            WebhookEnabled = true,
            WebhookFlavour = "github",
            WebhookEventsText = "push, pull_request",
            WebhookActionsText = "opened",
            WebhookBranchesText = "main, release/*",
            TargetKind = "cloud",
            RepositoriesText = "https://github.com/owner/app work main\nhttps://github.com/owner/lib",
            SecretRefsText = "API_KEY, API_KEY, TOKEN",
            EnvironmentId = " env-1 ",
            Model = "model-x",
            Mode = "plan",
            AllowPullRequestCreate = false,
            AllowPullRequestComment = true,
            AllowRequestReviewers = true,
            MaxConcurrentRunsText = "3",
            MaxRunsPerHourText = "20",
            MaxRuntimeMinutesText = "90",
            Enabled = false,
        };

        var draft = model.ToDraft();

        draft.Name.ShouldBe("Nightly triage", Case.Sensitive);
        draft.Prompt.ShouldBe("Triage new issues.\n", Case.Sensitive);
        draft.Enabled.ShouldBeFalse();
        draft.TimeZone.ShouldBe("Europe/Berlin", Case.Sensitive);
        draft.TargetKind.ShouldBe("cloud", Case.Sensitive);
        draft.Triggers.Count.ShouldBe(2);
        draft.Triggers[0].Kind.ShouldBe("cron", Case.Sensitive);
        draft.Triggers[0].Cron.ShouldBe("0 9 * * 1-5", Case.Sensitive);
        draft.Triggers[1].Kind.ShouldBe("webhook", Case.Sensitive);
        draft.Triggers[1].Flavour.ShouldBe("github", Case.Sensitive);
        draft.Triggers[1].Events.ShouldBe(["push", "pull_request"]);
        draft.Triggers[1].Actions.ShouldBe(["opened"]);
        draft.Triggers[1].Branches.ShouldBe(["main", "release/*"]);
        draft.Repositories.ShouldBe(
        [
            new AutomationRepositoryDraft("https://github.com/owner/app", "work", "main"),
            new AutomationRepositoryDraft("https://github.com/owner/lib", string.Empty, string.Empty),
        ]);
        draft.SecretRefs.ShouldBe(["API_KEY", "TOKEN"]);
        draft.EnvironmentId.ShouldBe("env-1", Case.Sensitive);
        draft.Model.ShouldBe("model-x", Case.Sensitive);
        draft.Mode.ShouldBe("plan", Case.Sensitive);
        draft.AllowedTools.ShouldBe(["pull_request_comment", "pull_request_request_reviewers"]);
        draft.MaxConcurrentRuns.ShouldBe(3);
        draft.MaxRunsPerHour.ShouldBe(20);
        draft.MaxRuntimeMinutes.ShouldBe(90);
    }

    /// <summary>An empty time zone means UTC, and a generic webhook carries no GitHub filters even when they were typed.</summary>
    [Fact]
    public void EmptyTimeZoneMeansUtcAndGenericWebhookDropsFilters()
    {
        var model = ValidLocal(new FakeAutomationsManager());
        model.TimeZone = "  ";
        model.WebhookEnabled = true;
        model.WebhookFlavour = "generic";
        model.WebhookEventsText = "push";

        var draft = model.ToDraft();

        draft.TimeZone.ShouldBe("UTC", Case.Sensitive);
        draft.Triggers[1].Events.ShouldBeEmpty();
    }

    /// <summary>Every property announces its own change and the model raises its single change event, so a head can redraw once.</summary>
    [Fact]
    public void EditsAnnounceTheirPropertyAndRaiseTheChangeEvent()
    {
        var model = new AutomationEditorModel(new FakeAutomationsManager(), null);
        var properties = new List<string>();
        var changed = 0;
        model.PropertyChanged += (_, args) => properties.Add(args.PropertyName ?? string.Empty);
        model.Changed += (_, _) => changed++;

        model.Name = "Release notes";
        model.Name = "Release notes";

        properties.ShouldContain("Name");
        properties.ShouldContain("CanSave");
        properties.ShouldContain("WhyDisabled");
        changed.ShouldBe(1);
    }

    private static string Repository(int number) =>
        "https://github.com/owner/repo" + number.ToString(CultureInfo.InvariantCulture);

    private static string Repositories(int count) =>
        string.Join('\n', Enumerable.Range(1, count).Select(Repository));

    private static AutomationEditorModel ValidLocal(FakeAutomationsManager manager) =>
        new(manager, null)
        {
            Name = "Nightly triage",
            Prompt = "Triage new issues.",
            CronText = "0 9 * * 1-5",
        };

    private static AutomationEditorModel ValidCloud(FakeAutomationsManager manager)
    {
        var model = ValidLocal(manager);
        model.TargetKind = "cloud";
        model.RepositoriesText = Repository(1);
        return model;
    }
}
