using System.Text.Json;
using OmpOS.Contracts.Automations;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Automations;

/// <summary>Verifies the automation shapes: camel-case wire names, omitted nulls, tolerance of unknown members in requests, and a byte-stable round trip through <see cref="AutomationsJsonContext"/>.</summary>
public sealed class AutomationContractJsonTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static AutomationInfo Info() =>
        new()
        {
            Id = "aut_1",
            Name = "Nightly review",
            Prompt = "Review.",
            Enabled = true,
            Triggers =
            [
                new AutomationTriggerInfo { Kind = "cron", Cron = "0 9 * * mon-fri" },
                new AutomationTriggerInfo { Kind = "webhook", Flavour = "github", Events = ["push"], Actions = [], Branches = ["main", "release/*"] },
            ],
            Target = new AutomationTargetInfo
            {
                Kind = "cloud",
                Repositories = [new AutomationRepositoryInfo { Address = "https://github.com/acme/widgets", Branch = "main" }],
                SecretRefs = ["NPM_TOKEN"],
            },
            AllowedTools = ["pull_request_create"],
            MaxConcurrentRuns = 1,
            MaxRunsPerHour = 12,
            TimeZone = "UTC",
            MaxRuntimeMinutes = 30,
            CreatedAt = Stamp,
            UpdatedAt = Stamp,
            NextRunAt = Stamp.AddDays(1),
            LastRun = new AutomationRunInfo { RunId = "r1", Trigger = "cron", Summary = "Scheduled run", Status = "launched", StartedAt = Stamp, FinishedAt = Stamp.AddSeconds(2), DurationSeconds = 2, ExternalId = "agent-1" },
            WebhookPath = "/hooks/automations/aut_1",
            HasWebhookSecret = true,
        };

    [Fact]
    public void An_automation_round_trips_with_camel_case_names_and_no_nulls()
    {
        var json = JsonSerializer.Serialize(Info(), AutomationsJsonContext.Default.AutomationInfo);

        json.ShouldContain("\"maxRunsPerHour\":12", Case.Sensitive);
        json.ShouldContain("\"webhookPath\":\"/hooks/automations/aut_1\"", Case.Sensitive);
        json.ShouldContain("\"hasWebhookSecret\":true", Case.Sensitive);
        json.ShouldNotContain("null", Case.Sensitive);
        json.ShouldNotContain("webhookSecret\"", Case.Sensitive);
        var back = JsonSerializer.Deserialize(json, AutomationsJsonContext.Default.AutomationInfo)!;
        JsonSerializer.Serialize(back, AutomationsJsonContext.Default.AutomationInfo).ShouldBe(json);
        back.Triggers[1].Branches.ShouldBe(["main", "release/*"]);
        back.LastRun!.DurationSeconds.ShouldBe(2);
    }

    [Fact]
    public void The_save_response_is_the_only_shape_with_a_secret_member_and_omits_it_when_there_is_none()
    {
        var with = JsonSerializer.Serialize(new AutomationSaveResponse { Automation = Info(), WebhookSecret = "whsec_abc" }, AutomationsJsonContext.Default.AutomationSaveResponse);
        var without = JsonSerializer.Serialize(new AutomationSaveResponse { Automation = Info() }, AutomationsJsonContext.Default.AutomationSaveResponse);

        with.ShouldContain("\"webhookSecret\":\"whsec_abc\"", Case.Sensitive);
        without.ShouldNotContain("webhookSecret\"", Case.Sensitive);
        typeof(AutomationInfo).GetProperties().Select(static property => property.Name).ShouldNotContain("WebhookSecret", StringComparer.Ordinal);
        typeof(AutomationRunInfo).GetProperties().Select(static property => property.Name).ShouldNotContain("WebhookSecret", StringComparer.Ordinal);
        typeof(AutomationListResponse).GetProperties().Select(static property => property.Name).ShouldNotContain("WebhookSecret", StringComparer.Ordinal);
    }

    [Fact]
    public void A_save_request_reads_defaults_as_absent_and_ignores_members_it_does_not_know()
    {
        const string Json = """{"name":"n","prompt":"p","triggers":[{"kind":"webhook","somethingNew":1}],"future":{"a":1}}""";

        var request = JsonSerializer.Deserialize(Json, AutomationsJsonContext.Default.SaveAutomationRequest)!;

        request.Name.ShouldBe("n");
        request.Enabled.ShouldBeNull();
        request.Target.ShouldBeNull();
        request.MaxRunsPerHour.ShouldBeNull();
        request.Triggers!.ShouldHaveSingleItem().Kind.ShouldBe("webhook");
        request.Triggers[0].Flavour.ShouldBeNull();
    }

    [Fact]
    public void A_run_list_and_a_cron_preview_use_the_documented_names()
    {
        var runs = JsonSerializer.Serialize(
            new AutomationRunListResponse { Runs = [new AutomationRunInfo { RunId = "r1", Trigger = "webhook", Summary = "s", Status = "timed_out", StartedAt = Stamp, Error = "e" }] },
            AutomationsJsonContext.Default.AutomationRunListResponse);
        var preview = JsonSerializer.Serialize(new AutomationCronPreviewResponse { NextRuns = [Stamp], Error = null }, AutomationsJsonContext.Default.AutomationCronPreviewResponse);
        var hook = JsonSerializer.Serialize(new AutomationWebhookResponse { Status = "accepted", RunId = "r1" }, AutomationsJsonContext.Default.AutomationWebhookResponse);

        runs.ShouldContain("\"status\":\"timed_out\"", Case.Sensitive);
        runs.ShouldNotContain("finishedAt", Case.Sensitive);
        preview.ShouldBe("{\"nextRuns\":[\"2026-10-28T09:00:00+00:00\"]}");
        hook.ShouldBe("{\"status\":\"accepted\",\"runId\":\"r1\"}");
    }
}
