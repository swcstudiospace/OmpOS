using OmpOS.Harness.Automations.Hosted;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>Verifies each rule of the automation validator at its boundary, and that a message never repeats what was typed.</summary>
public sealed class AutomationValidatorTests
{
    private static string[] Fields(UserAutomation automation) => AutomationValidator.Validate(automation).Select(static issue => issue.Field).ToArray();

    [Fact]
    public void A_valid_automation_has_no_issue()
    {
        AutomationValidator.Validate(TestAutomations.Make()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void The_name_is_one_to_one_hundred_characters(int length, bool valid)
    {
        var automation = TestAutomations.Make() with { Name = new string('n', length) };

        Fields(automation).Contains("name", StringComparer.Ordinal).ShouldBe(!valid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    public void A_blank_name_or_one_with_a_control_character_is_refused(string name)
    {
        Fields(TestAutomations.Make() with { Name = name }).ShouldContain("name");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(20_000, true)]
    [InlineData(20_001, false)]
    public void The_prompt_is_one_to_twenty_thousand_characters(int length, bool valid)
    {
        var automation = TestAutomations.Make() with { Prompt = new string('p', length) };

        Fields(automation).Contains("prompt", StringComparer.Ordinal).ShouldBe(!valid);
    }

    [Fact]
    public void A_blank_prompt_is_refused()
    {
        Fields(TestAutomations.Make() with { Prompt = "  " }).ShouldContain("prompt");
    }

    [Theory]
    [InlineData("UTC", true)]
    [InlineData("utc", true)]
    [InlineData("Mars/Olympus", false)]
    [InlineData("", false)]
    public void The_time_zone_must_be_known_to_the_host(string zone, bool valid)
    {
        Fields(TestAutomations.Make() with { TimeZone = zone }).Contains("timeZone", StringComparer.Ordinal).ShouldBe(!valid);
    }

    [Fact]
    public void Between_one_and_eight_triggers_are_required()
    {
        Fields(TestAutomations.Make([])).ShouldContain("triggers");
        Fields(TestAutomations.Make(Enumerable.Repeat(TestAutomations.Cron("0 9 * * *"), 8).ToArray())).ShouldNotContain("triggers");
        Fields(TestAutomations.Make(Enumerable.Repeat(TestAutomations.Cron("0 9 * * *"), 9).ToArray())).ShouldContain("triggers");
    }

    [Fact]
    public void A_cron_trigger_with_an_invalid_expression_is_refused_with_the_parser_message()
    {
        var issues = AutomationValidator.Validate(TestAutomations.Make([TestAutomations.Cron("61 * * * *")]));

        var issue = issues.ShouldHaveSingleItem();
        issue.Field.ShouldBe("cron", Case.Sensitive);
        issue.Message.ShouldContain("minute", Case.Sensitive);
        issue.Message.ShouldNotContain("61", Case.Sensitive);
    }

    [Fact]
    public void Only_one_webhook_trigger_of_each_flavour_is_allowed()
    {
        Fields(TestAutomations.Make([TestAutomations.GenericHook(), TestAutomations.GitHubHook()])).ShouldNotContain("webhook");
        Fields(TestAutomations.Make([TestAutomations.GenericHook(), TestAutomations.GenericHook()])).ShouldContain("webhook");
        Fields(TestAutomations.Make([TestAutomations.GitHubHook(), TestAutomations.GitHubHook(events: ["push"])])).ShouldContain("webhook");
    }

    [Fact]
    public void Filters_belong_to_the_github_flavour()
    {
        var generic = new AutomationTriggerSpec { Kind = AutomationTriggerKind.Webhook, Flavour = WebhookFlavour.Generic, Branches = ["main"] };

        Fields(TestAutomations.Make([generic])).ShouldContain("webhook");
    }

    [Theory]
    [InlineData("push", true)]
    [InlineData("pull_request", true)]
    [InlineData("issues", false)]
    [InlineData("Push", false)]
    public void Only_the_push_and_pull_request_events_can_be_listed(string eventName, bool valid)
    {
        Fields(TestAutomations.Make([TestAutomations.GitHubHook(events: [eventName])])).Contains("webhook", StringComparer.Ordinal).ShouldBe(!valid);
    }

    [Theory]
    [InlineData("main", true)]
    [InlineData("release/*", true)]
    [InlineData("feature/**", true)]
    [InlineData("a b", false)]
    [InlineData("-x", false)]
    [InlineData("a..b", false)]
    [InlineData("a\\b", false)]
    [InlineData("a:b", false)]
    [InlineData("/lead", false)]
    [InlineData("trail/", false)]
    public void Branch_globs_are_checked_for_git_unsafe_characters(string glob, bool valid)
    {
        Fields(TestAutomations.Make([TestAutomations.GitHubHook(branches: [glob])])).Contains("webhook", StringComparer.Ordinal).ShouldBe(!valid);
    }

    [Fact]
    public void A_filter_list_with_a_repeat_or_more_than_sixteen_entries_is_refused()
    {
        Fields(TestAutomations.Make([TestAutomations.GitHubHook(actions: ["opened", "opened"])])).ShouldContain("webhook");
        var seventeen = Enumerable.Range(0, 17).Select(static index => "a" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Fields(TestAutomations.Make([TestAutomations.GitHubHook(actions: seventeen)])).ShouldContain("webhook");
        Fields(TestAutomations.Make([TestAutomations.GitHubHook(actions: seventeen[..16])])).ShouldNotContain("webhook");
    }

    [Fact]
    public void A_cloud_target_needs_between_one_and_sixteen_repositories()
    {
        UserAutomation With(int count) => TestAutomations.Make() with
        {
            Target = new AutomationTargetSpec
            {
                Repositories = Enumerable.Range(0, count).Select(static index => new AutomationRepository("https://github.com/acme/r" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray(),
            },
        };

        Fields(With(0)).ShouldContain("repositories");
        Fields(With(1)).ShouldNotContain("repositories");
        Fields(With(16)).ShouldNotContain("repositories");
        Fields(With(17)).ShouldContain("repositories");
    }

    [Theory]
    [InlineData("https://github.com/acme/widgets", true)]
    [InlineData("http://github.com/acme/widgets", false)]
    [InlineData("https://user:pass@github.com/acme/widgets", false)]
    [InlineData("https://github.com/acme/widgets?token=1", false)]
    [InlineData("https://github.com/acme/widgets#frag", false)]
    [InlineData("https://github.com", false)]
    [InlineData("git@github.com:acme/widgets.git", false)]
    [InlineData("not a url", false)]
    public void Repository_urls_are_https_without_credentials_query_or_fragment(string url, bool valid)
    {
        var automation = TestAutomations.Make() with { Target = new AutomationTargetSpec { Repositories = [new AutomationRepository(url)] } };

        Fields(automation).Contains("repositories", StringComparer.Ordinal).ShouldBe(!valid);
    }

    [Fact]
    public void The_message_about_a_repository_never_repeats_the_url()
    {
        var automation = TestAutomations.Make() with { Target = new AutomationTargetSpec { Repositories = [new AutomationRepository("https://user:hunter2@github.com/acme/widgets")] } };

        AutomationValidator.Validate(automation).Single().Message.ShouldNotContain("hunter2", Case.Insensitive);
    }

    [Fact]
    public void A_local_target_takes_no_repositories_secrets_or_environment()
    {
        var local = TestAutomations.Make() with { Target = new AutomationTargetSpec { Kind = AutomationTargetKind.Local } };

        Fields(local).ShouldBeEmpty();
        Fields(local with { Target = local.Target with { Repositories = [new AutomationRepository("https://github.com/acme/widgets")] } }).ShouldContain("target");
        Fields(local with { Target = local.Target with { SecretRefs = ["TOKEN"] } }).ShouldContain("target");
        Fields(local with { Target = local.Target with { EnvironmentId = "dev" } }).ShouldContain("target");
    }

    [Theory]
    [InlineData("TOKEN", true)]
    [InlineData("_private1", true)]
    [InlineData("1TOKEN", false)]
    [InlineData("TO-KEN", false)]
    [InlineData("TO KEN", false)]
    [InlineData("", false)]
    public void Secret_names_are_identifiers(string name, bool valid)
    {
        var automation = TestAutomations.Make();
        automation = automation with { Target = automation.Target with { SecretRefs = [name] } };

        Fields(automation).Contains("secrets", StringComparer.Ordinal).ShouldBe(!valid);
    }

    [Fact]
    public void Secret_names_must_be_distinct_and_at_most_thirty_two()
    {
        var automation = TestAutomations.Make();

        Fields(automation with { Target = automation.Target with { SecretRefs = ["A", "A"] } }).ShouldContain("secrets");
        var thirtyThree = Enumerable.Range(0, 33).Select(static index => "S" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Fields(automation with { Target = automation.Target with { SecretRefs = thirtyThree } }).ShouldContain("secrets");
        Fields(automation with { Target = automation.Target with { SecretRefs = thirtyThree[..32] } }).ShouldNotContain("secrets");
        Fields(automation with { Target = automation.Target with { SecretRefs = ["A" + new string('x', 64)] } }).ShouldContain("secrets");
    }

    [Fact]
    public void Only_known_tools_may_be_allowed_and_each_once()
    {
        var automation = TestAutomations.Make();

        Fields(automation with { AllowedTools = [] }).ShouldBeEmpty();
        Fields(automation with { AllowedTools = [.. AutomationTools.Known] }).ShouldBeEmpty();
        Fields(automation with { AllowedTools = ["shell"] }).ShouldContain("tools");
        Fields(automation with { AllowedTools = [AutomationTools.PullRequestCreate, AutomationTools.PullRequestCreate] }).ShouldContain("tools");
    }

    [Theory]
    [InlineData(0, 12, 30, false)]
    [InlineData(1, 12, 30, true)]
    [InlineData(4, 12, 30, true)]
    [InlineData(5, 12, 30, false)]
    [InlineData(1, 0, 30, false)]
    [InlineData(1, 60, 30, true)]
    [InlineData(1, 61, 30, false)]
    [InlineData(1, 12, 0, false)]
    [InlineData(1, 12, 360, true)]
    [InlineData(1, 12, 361, false)]
    public void The_limits_are_checked_at_their_boundaries(int concurrent, int perHour, int minutes, bool valid)
    {
        var automation = TestAutomations.Make() with { MaxConcurrentRuns = concurrent, MaxRunsPerHour = perHour, MaxRuntimeMinutes = minutes };

        Fields(automation).Contains("limits", StringComparer.Ordinal).ShouldBe(!valid);
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var automation = TestAutomations.Make([]) with { Name = string.Empty, Prompt = string.Empty, MaxRunsPerHour = 0 };

        Fields(automation).ShouldBe(["name", "prompt", "triggers", "limits"], ignoreOrder: true);
    }
}
