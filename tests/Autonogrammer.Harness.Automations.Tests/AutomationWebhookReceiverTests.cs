using Autonogrammer.Harness.Automations.Hosted;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Automations.Tests;

/// <summary>Verifies the receiver logic behind the public URL: lookup, secret, trigger decision and the run it starts.</summary>
public sealed class AutomationWebhookReceiverTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _clock = new(Kit.Start);
    private readonly InMemoryAutomationStore _store = new();
    private readonly InMemoryAutomationSecretStore _secrets = new();
    private readonly ScriptedTarget _target = new();

    private async Task<(AutomationWebhookReceiver Receiver, AutomationRunner Runner, UserAutomation Automation)> ArrangeAsync(UserAutomation? automation = null)
    {
        var definition = automation ?? TestAutomations.Make([TestAutomations.GenericHook()]);
        await _store.SaveAsync(definition, Token).ConfigureAwait(false);
        await _secrets.SetAsync(AutomationSecrets.WebhookReference(definition.Id), WebhookSender.Secret, Token).ConfigureAwait(false);
        var runner = new AutomationRunner(_store, [_target], _clock);
        return (new AutomationWebhookReceiver(_store, _secrets, new WebhookTrigger(_clock), runner), runner, definition);
    }

    [Fact]
    public async Task An_authentic_delivery_starts_a_run_whose_history_names_the_webhook()
    {
        var (receiver, runner, automation) = await ArrangeAsync().ConfigureAwait(true);
        var scope = runner;
        await using var scopeScope = scope.ConfigureAwait(true);

        var receipt = await receiver.HandleAsync(automation.Id, WebhookSender.Generic("""{"a":1}""", _clock.GetUtcNow()), Token).ConfigureAwait(true);

        receipt.Outcome.ShouldBe(WebhookOutcome.Accepted);
        receipt.RunId.ShouldNotBeNull();
        receipt.Refusal.ShouldBeNull();
        while ((await _store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true)).Any(static entry => entry.Status == AutomationRunStatus.Running))
        {
            await Task.Yield();
        }

        var entry = (await _store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true)).ShouldHaveSingleItem();
        entry.Source.ShouldBe(AutomationRunSource.Webhook);
        entry.RunId.ShouldBe(receipt.RunId);
        _target.Requests.ShouldHaveSingleItem().Prompt.ShouldContain("""{"a":1}""", Case.Sensitive);
    }

    [Theory]
    [InlineData("aut_missing")]
    [InlineData("../../etc/passwd")]
    public async Task An_unknown_automation_is_unauthorized_and_starts_nothing(string id)
    {
        var (receiver, runner, _) = await ArrangeAsync().ConfigureAwait(true);
        var scope = runner;
        await using var scopeScope = scope.ConfigureAwait(true);

        var receipt = await receiver.HandleAsync(id, WebhookSender.Generic("{}", _clock.GetUtcNow()), Token).ConfigureAwait(true);

        receipt.Outcome.ShouldBe(WebhookOutcome.Unauthorized);
        receipt.RunId.ShouldBeNull();
        _target.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_forged_delivery_starts_nothing()
    {
        var (receiver, runner, automation) = await ArrangeAsync().ConfigureAwait(true);
        var scope = runner;
        await using var scopeScope = scope.ConfigureAwait(true);

        var receipt = await receiver.HandleAsync(automation.Id, WebhookSender.Generic("{}", _clock.GetUtcNow(), secret: "whsec_wrong"), Token).ConfigureAwait(true);

        receipt.Outcome.ShouldBe(WebhookOutcome.Unauthorized);
        _target.Requests.ShouldBeEmpty();
        (await _store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_deleted_secret_means_the_automation_no_longer_accepts_deliveries()
    {
        var (receiver, runner, automation) = await ArrangeAsync().ConfigureAwait(true);
        var scope = runner;
        await using var scopeScope = scope.ConfigureAwait(true);
        await _secrets.DeleteAsync(AutomationSecrets.WebhookReference(automation.Id), Token).ConfigureAwait(true);

        var receipt = await receiver.HandleAsync(automation.Id, WebhookSender.Generic("{}", _clock.GetUtcNow()), Token).ConfigureAwait(true);

        receipt.Outcome.ShouldBe(WebhookOutcome.Unauthorized);
    }

    [Fact]
    public async Task An_authentic_delivery_that_a_guard_refuses_reports_the_guard_and_the_run_is_skipped()
    {
        var (receiver, runner, automation) = await ArrangeAsync().ConfigureAwait(true);
        var scope = runner;
        await using var scopeScope = scope.ConfigureAwait(true);
        _target.Hold = true;

        var first = await receiver.HandleAsync(automation.Id, WebhookSender.Generic("{}", _clock.GetUtcNow(), delivery: "one"), Token).ConfigureAwait(true);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var second = await receiver.HandleAsync(automation.Id, WebhookSender.Generic("{}", _clock.GetUtcNow(), delivery: "two"), Token).ConfigureAwait(true);

        first.Refusal.ShouldBeNull();
        second.Outcome.ShouldBe(WebhookOutcome.Accepted);
        second.Refusal.ShouldBe(AutomationRunRefusal.ConcurrencyLimit);
        second.RunId.ShouldBeNull();
        (await _store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true))[0].Status.ShouldBe(AutomationRunStatus.Skipped);
        await _target.ReleaseWhenHeldAsync(1, Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task A_github_ping_and_an_ignored_event_start_nothing()
    {
        var automation = TestAutomations.Make([TestAutomations.GitHubHook(branches: ["main"])]);
        var (receiver, runner, _) = await ArrangeAsync(automation).ConfigureAwait(true);
        var scope = runner;
        await using var scopeScope = scope.ConfigureAwait(true);

        var ping = await receiver.HandleAsync(automation.Id, WebhookSender.GitHub("ping", "{}", delivery: "a"), Token).ConfigureAwait(true);
        var other = await receiver.HandleAsync(automation.Id, WebhookSender.GitHub("push", WebhookSender.PushPayload("develop"), delivery: "b"), Token).ConfigureAwait(true);

        ping.Outcome.ShouldBe(WebhookOutcome.Ping);
        other.Outcome.ShouldBe(WebhookOutcome.Ignored);
        _target.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_disabled_automation_ignores_deliveries_and_starts_nothing()
    {
        var automation = TestAutomations.Make([TestAutomations.GenericHook()], enabled: false);
        var (receiver, runner, _) = await ArrangeAsync(automation).ConfigureAwait(true);
        var scope = runner;
        await using var scopeScope = scope.ConfigureAwait(true);

        var receipt = await receiver.HandleAsync(automation.Id, WebhookSender.Generic("{}", _clock.GetUtcNow()), Token).ConfigureAwait(true);

        receipt.Outcome.ShouldBe(WebhookOutcome.Ignored);
        _target.Requests.ShouldBeEmpty();
    }
}
