using OmpOS.Harness.Automations.Hosted;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Verifies the automation service: owner scoping, validation, the webhook secret lifecycle (shown once, kept in step with the triggers),
/// and the canary that a generated secret appears nowhere but the one result that created it.
/// </summary>
public sealed class AutomationServiceTests
{
    private const string Bob = "bob";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _clock = new(Kit.Start);
    private readonly InMemoryAutomationStore _store = new();
    private readonly InMemoryAutomationSecretStore _secrets = new();
    private readonly AutomationService _service;

    public AutomationServiceTests()
    {
        _service = new AutomationService(_store, _secrets, _clock);
    }

    [Fact]
    public async Task Create_stamps_an_unguessable_id_the_owner_and_the_times_and_applies_defaults()
    {
        var change = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(), Token).ConfigureAwait(true);

        change.Succeeded.ShouldBeTrue();
        var automation = change.Automation.ShouldNotBeNull();
        automation.Id.ShouldStartWith("aut_", Case.Sensitive);
        automation.Id.Length.ShouldBe(4 + 32);
        automation.Owner.ShouldBe(TestAutomations.Alice, Case.Sensitive);
        automation.CreatedAt.ShouldBe(Kit.Start);
        automation.UpdatedAt.ShouldBe(Kit.Start);
        automation.AllowedTools.ShouldBe([AutomationTools.PullRequestCreate]);
        automation.MaxConcurrentRuns.ShouldBe(1);
        automation.MaxRunsPerHour.ShouldBe(AutomationLimits.DefaultMaxRunsPerHour);
        automation.MaxRuntimeMinutes.ShouldBe(AutomationLimits.DefaultMaxRuntimeMinutes);
        change.WebhookSecret.ShouldBeNull();
        (await _store.GetAsync(automation.Id, Token).ConfigureAwait(true)).ShouldBe(automation);
    }

    [Fact]
    public async Task Two_automations_never_share_an_id()
    {
        var first = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(), Token).ConfigureAwait(true);
        var second = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(), Token).ConfigureAwait(true);

        first.Automation!.Id.ShouldNotBe(second.Automation!.Id, Case.Sensitive);
    }

    [Fact]
    public async Task Create_trims_the_name()
    {
        var change = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft() with { Name = "  Padded  " }, Token).ConfigureAwait(true);

        change.Automation!.Name.ShouldBe("Padded", Case.Sensitive);
    }

    [Fact]
    public async Task An_invalid_draft_is_refused_with_every_issue_and_stores_nothing()
    {
        var change = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft() with { Name = string.Empty, MaxRunsPerHour = 0 }, Token).ConfigureAwait(true);

        change.Succeeded.ShouldBeFalse();
        change.NotFound.ShouldBeFalse();
        change.Issues.Select(static issue => issue.Field).ShouldBe(["name", "limits"], ignoreOrder: true);
        (await _store.ListAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_owner_may_have_at_most_fifty_automations()
    {
        for (var index = 0; index < AutomationLimits.MaxAutomationsPerOwner; index++)
        {
            (await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        }

        var over = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(), Token).ConfigureAwait(true);
        var other = await _service.CreateAsync(Bob, TestAutomations.Draft(), Token).ConfigureAwait(true);

        over.Issues.ShouldContain(static issue => string.Equals(issue.Field, "limits", StringComparison.Ordinal));
        other.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task An_owner_sees_only_their_own_automations_and_another_owners_reads_as_absent()
    {
        var alices = (await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(), Token).ConfigureAwait(true)).Automation!;
        var bobs = (await _service.CreateAsync(Bob, TestAutomations.Draft(), Token).ConfigureAwait(true)).Automation!;

        (await _service.ListAsync(TestAutomations.Alice, Token).ConfigureAwait(true)).Select(static automation => automation.Id).ShouldBe([alices.Id]);
        (await _service.GetAsync(Bob, alices.Id, Token).ConfigureAwait(true)).ShouldBeNull();
        (await _service.GetAsync(TestAutomations.Alice, bobs.Id, Token).ConfigureAwait(true)).ShouldBeNull();
        (await _service.ReplaceAsync(Bob, alices.Id, TestAutomations.Draft(), Token).ConfigureAwait(true)).NotFound.ShouldBeTrue();
        (await _service.SetEnabledAsync(Bob, alices.Id, false, Token).ConfigureAwait(true)).NotFound.ShouldBeTrue();
        (await _service.RotateWebhookSecretAsync(Bob, alices.Id, Token).ConfigureAwait(true)).NotFound.ShouldBeTrue();
        (await _service.DeleteAsync(Bob, alices.Id, Token).ConfigureAwait(true)).ShouldBeFalse();
        (await _store.GetAsync(alices.Id, Token).ConfigureAwait(true)).ShouldNotBeNull();
    }

    [Fact]
    public async Task The_list_is_newest_first()
    {
        var first = (await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(), Token).ConfigureAwait(true)).Automation!;
        _clock.Advance(TimeSpan.FromMinutes(1));
        var second = (await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(), Token).ConfigureAwait(true)).Automation!;

        (await _service.ListAsync(TestAutomations.Alice, Token).ConfigureAwait(true)).Select(static automation => automation.Id).ShouldBe([second.Id, first.Id]);
    }

    [Fact]
    public async Task A_webhook_trigger_gets_a_secret_that_is_returned_once_and_stored_by_reference()
    {
        var change = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);

        var secret = change.WebhookSecret.ShouldNotBeNull();
        secret.ShouldStartWith("whsec_", Case.Sensitive);
        secret.Length.ShouldBeGreaterThanOrEqualTo(40);
        (await _secrets.GetAsync(AutomationSecrets.WebhookReference(change.Automation!.Id), Token).ConfigureAwait(true)).ShouldBe(secret, Case.Sensitive);
        (await _service.HasWebhookSecretAsync(change.Automation, Token).ConfigureAwait(true)).ShouldBeTrue();
        change.Automation.HasWebhook.ShouldBeTrue();
    }

    [Fact]
    public async Task Two_automations_never_share_a_secret()
    {
        var one = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);
        var two = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);

        one.WebhookSecret.ShouldNotBe(two.WebhookSecret, Case.Sensitive);
    }

    [Fact]
    public async Task Replace_keeps_the_secret_when_a_webhook_stays_and_returns_none()
    {
        var created = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);
        var id = created.Automation!.Id;

        _clock.Advance(TimeSpan.FromHours(1));
        var replaced = await _service.ReplaceAsync(TestAutomations.Alice, id, TestAutomations.Draft(TestAutomations.GenericHook(), TestAutomations.Cron("0 8 * * *")) with { Name = "Renamed" }, Token).ConfigureAwait(true);

        replaced.Succeeded.ShouldBeTrue();
        replaced.WebhookSecret.ShouldBeNull();
        replaced.Automation!.Name.ShouldBe("Renamed", Case.Sensitive);
        replaced.Automation.Id.ShouldBe(id, Case.Sensitive);
        replaced.Automation.CreatedAt.ShouldBe(Kit.Start);
        replaced.Automation.UpdatedAt.ShouldBe(Kit.Start.AddHours(1));
        (await _secrets.GetAsync(AutomationSecrets.WebhookReference(id), Token).ConfigureAwait(true)).ShouldBe(created.WebhookSecret, Case.Sensitive);
    }

    [Fact]
    public async Task Replace_generates_a_secret_when_the_first_webhook_is_added_and_deletes_it_when_the_last_goes()
    {
        var created = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(), Token).ConfigureAwait(true);
        var id = created.Automation!.Id;
        (await _service.HasWebhookSecretAsync(created.Automation, Token).ConfigureAwait(true)).ShouldBeFalse();

        var added = await _service.ReplaceAsync(TestAutomations.Alice, id, TestAutomations.Draft(TestAutomations.GitHubHook()), Token).ConfigureAwait(true);

        added.WebhookSecret.ShouldNotBeNull();
        var removed = await _service.ReplaceAsync(TestAutomations.Alice, id, TestAutomations.Draft(), Token).ConfigureAwait(true);
        removed.WebhookSecret.ShouldBeNull();
        (await _secrets.GetAsync(AutomationSecrets.WebhookReference(id), Token).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task An_invalid_replacement_changes_nothing()
    {
        var created = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);
        var id = created.Automation!.Id;

        var refused = await _service.ReplaceAsync(TestAutomations.Alice, id, TestAutomations.Draft() with { Prompt = string.Empty }, Token).ConfigureAwait(true);

        refused.Succeeded.ShouldBeFalse();
        (await _store.GetAsync(id, Token).ConfigureAwait(true)).ShouldBe(created.Automation);
        (await _secrets.GetAsync(AutomationSecrets.WebhookReference(id), Token).ConfigureAwait(true)).ShouldBe(created.WebhookSecret, Case.Sensitive);
    }

    [Fact]
    public async Task Rotating_replaces_the_secret_and_the_old_one_stops_working()
    {
        var created = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);
        var automation = created.Automation!;

        var rotated = await _service.RotateWebhookSecretAsync(TestAutomations.Alice, automation.Id, Token).ConfigureAwait(true);

        var fresh = rotated.WebhookSecret.ShouldNotBeNull();
        fresh.ShouldNotBe(created.WebhookSecret, Case.Sensitive);
        var trigger = new WebhookTrigger(_clock);
        var oldSignature = WebhookSender.Generic("{}", _clock.GetUtcNow(), created.WebhookSecret!);
        var newSignature = WebhookSender.Generic("{}", _clock.GetUtcNow(), fresh);
        var stored = await _secrets.GetAsync(AutomationSecrets.WebhookReference(automation.Id), Token).ConfigureAwait(true);
        trigger.Evaluate(automation, stored, oldSignature).Outcome.ShouldBe(WebhookOutcome.Unauthorized);
        trigger.Evaluate(automation, stored, newSignature).Outcome.ShouldBe(WebhookOutcome.Accepted);
    }

    [Fact]
    public async Task Rotating_an_automation_without_a_webhook_is_refused()
    {
        var created = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(), Token).ConfigureAwait(true);

        var rotated = await _service.RotateWebhookSecretAsync(TestAutomations.Alice, created.Automation!.Id, Token).ConfigureAwait(true);

        rotated.Succeeded.ShouldBeFalse();
        rotated.Issues.ShouldContain(static issue => string.Equals(issue.Field, "webhook", StringComparison.Ordinal));
        rotated.NotFound.ShouldBeFalse();
    }

    [Fact]
    public async Task SetEnabled_turns_triggers_on_and_off_and_stamps_the_change()
    {
        var created = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft() with { Enabled = true }, Token).ConfigureAwait(true);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var off = await _service.SetEnabledAsync(TestAutomations.Alice, created.Automation!.Id, false, Token).ConfigureAwait(true);

        off.Automation!.Enabled.ShouldBeFalse();
        off.Automation.UpdatedAt.ShouldBe(Kit.Start.AddMinutes(5));
        off.WebhookSecret.ShouldBeNull();
        (await _service.GetAsync(TestAutomations.Alice, created.Automation.Id, Token).ConfigureAwait(true))!.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_removes_the_automation_its_history_and_its_secret()
    {
        var created = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);
        var id = created.Automation!.Id;
        await _store.UpsertRunAsync(id, new AutomationRunEntry { RunId = "r1", Source = AutomationRunSource.Manual, Status = AutomationRunStatus.Succeeded, StartedAt = Kit.Start }, Token).ConfigureAwait(true);

        (await _service.DeleteAsync(TestAutomations.Alice, id, Token).ConfigureAwait(true)).ShouldBeTrue();

        (await _store.GetAsync(id, Token).ConfigureAwait(true)).ShouldBeNull();
        (await _store.GetHistoryAsync(id, Token).ConfigureAwait(true)).ShouldBeEmpty();
        (await _secrets.GetAsync(AutomationSecrets.WebhookReference(id), Token).ConfigureAwait(true)).ShouldBeNull();
        (await _service.DeleteAsync(TestAutomations.Alice, id, Token).ConfigureAwait(true)).ShouldBeFalse();
    }

    [Fact]
    public async Task The_secret_appears_only_in_the_result_that_created_it()
    {
        var created = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);
        var secret = created.WebhookSecret.ShouldNotBeNull();
        var id = created.Automation!.Id;
        var surfaces = new List<string>
        {
            created.Automation.ToString(),
            created.ToString()!,
            string.Join('|', (await _service.ListAsync(TestAutomations.Alice, Token).ConfigureAwait(true)).Select(static automation => automation.ToString())),
            (await _service.GetAsync(TestAutomations.Alice, id, Token).ConfigureAwait(true))!.ToString(),
            (await _service.SetEnabledAsync(TestAutomations.Alice, id, true, Token).ConfigureAwait(true)).ToString()!,
            (await _service.ReplaceAsync(TestAutomations.Alice, id, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true)).ToString()!,
        };
        var replaced = await _service.ReplaceAsync(TestAutomations.Alice, id, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);
        replaced.WebhookSecret.ShouldBeNull();
        var history = await _store.GetHistoryAsync(id, Token).ConfigureAwait(true);
        surfaces.AddRange(history.Select(static entry => entry.ToString()));

        foreach (var surface in surfaces)
        {
            surface.ShouldNotContain(secret, Case.Sensitive);
        }

        // The one place a person ever sees it again is a rotation, which replaces it.
        (await _service.RotateWebhookSecretAsync(TestAutomations.Alice, id, Token).ConfigureAwait(true)).WebhookSecret.ShouldNotBe(secret, Case.Sensitive);
    }

    [Fact]
    public async Task A_secret_never_reaches_the_prompt_the_history_or_the_error_of_a_run()
    {
        var created = await _service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);
        var secret = created.WebhookSecret.ShouldNotBeNull();
        var automation = created.Automation!;
        var target = new ScriptedTarget { Throw = new InvalidOperationException("boom") };
        await using var runner = new AutomationRunner(_store, [target], _clock);
        var receiver = new AutomationWebhookReceiver(_store, _secrets, new WebhookTrigger(_clock), runner);

        var receipt = await receiver.HandleAsync(automation.Id, WebhookSender.Generic("""{"event":"deploy"}""", _clock.GetUtcNow(), secret), Token).ConfigureAwait(true);
        receipt.RunId.ShouldNotBeNull();
        while ((await _store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true)).Any(static entry => entry.Status == AutomationRunStatus.Running))
        {
            await Task.Yield();
        }

        var start = await runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        _ = await start.Completion!.ConfigureAwait(true);

        receipt.Outcome.ShouldBe(WebhookOutcome.Accepted);
        foreach (var request in target.Requests)
        {
            request.Prompt.ShouldNotContain(secret, Case.Sensitive);
            request.Automation.ToString().ShouldNotContain(secret, Case.Sensitive);
        }

        foreach (var entry in await _store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true))
        {
            entry.ToString().ShouldNotContain(secret, Case.Sensitive);
        }

        receipt.ToString().ShouldNotContain(secret, Case.Sensitive);
    }
}
