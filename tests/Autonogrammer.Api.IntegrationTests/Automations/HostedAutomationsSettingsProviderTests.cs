using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Api.Automations;
using Autonogrammer.Api.Security;
using Autonogrammer.Harness.Automations.Hosted;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Automations;

/// <summary>Verifies the settings seam: the existing summary route lists the caller's own automations, with the last result mapped to its words.</summary>
public sealed class HostedAutomationsSettingsProviderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AutomationClock _clock = new(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemoryAutomationStore _store = new();
    private readonly InMemoryAutomationSecretStore _secrets = new();

    private static HttpContextAccessor AccessorFor(string? owner)
    {
        if (owner is null)
        {
            // The accessor keeps its context in a static async-local: a new one would see the context an earlier accessor of this flow set.
            return new HttpContextAccessor { HttpContext = null };
        }

        var services = new ServiceCollection();
        services.AddSingleton<IUserContext>(new FixedUser(owner));
        var provider = services.BuildServiceProvider();
        return new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = provider, User = new ClaimsPrincipal() } };
    }

    private HostedAutomationsSettingsProvider Provider(string? owner, bool enabled = true) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new AutomationOptions { Enabled = enabled }),
            AccessorFor(owner),
            new AutomationService(_store, _secrets, _clock),
            _store);

    [Fact]
    public async Task It_is_not_configured_when_automations_are_off_or_no_request_is_in_scope()
    {
        (await Provider("alice", enabled: false).ListAsync(Token).ConfigureAwait(true)).ShouldBeNull();
        (await Provider(owner: null).ListAsync(Token).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task It_lists_only_the_callers_automations_with_their_triggers_in_words_and_runtime_in_seconds()
    {
        var service = new AutomationService(_store, _secrets, _clock);
        var draft = new AutomationDraft
        {
            Name = "n",
            Prompt = "p",
            Enabled = true,
            Triggers =
            [
                new AutomationTriggerSpec { Kind = AutomationTriggerKind.Cron, Cron = "0 9 * * *" },
                new AutomationTriggerSpec { Kind = AutomationTriggerKind.Webhook, Flavour = WebhookFlavour.GitHub },
            ],
            Target = new AutomationTargetSpec { Repositories = [new AutomationRepository("https://github.com/acme/widgets")] },
            MaxRuntimeMinutes = 20,
        };
        var alices = (await service.CreateAsync("alice", draft, Token).ConfigureAwait(true)).Automation!;
        _ = await service.CreateAsync("bob", draft, Token).ConfigureAwait(true);

        var list = await Provider("alice").ListAsync(Token).ConfigureAwait(true);

        var info = list.ShouldNotBeNull().ShouldHaveSingleItem();
        info.Id.ShouldBe(alices.Id);
        info.Trigger.ShouldBe("cron 0 9 * * *; GitHub webhook");
        info.Enabled.ShouldBeTrue();
        info.MaxRuntimeSeconds.ShouldBe(1200);
        info.LastRun.ShouldBeNull();
        info.Pause.ShouldBeNull();
    }

    [Theory]
    [InlineData(AutomationRunStatus.Succeeded, "succeeded")]
    [InlineData(AutomationRunStatus.Launched, "succeeded")]
    [InlineData(AutomationRunStatus.Failed, "failed")]
    [InlineData(AutomationRunStatus.TimedOut, "timed-out")]
    [InlineData(AutomationRunStatus.Cancelled, "cancelled")]
    [InlineData(AutomationRunStatus.Skipped, "refused")]
    public async Task The_last_finished_run_is_mapped_to_the_words_of_the_summary_contract(AutomationRunStatus status, string outcome)
    {
        var service = new AutomationService(_store, _secrets, _clock);
        var created = (await service.CreateAsync("alice", new AutomationDraft { Name = "n", Prompt = "p", Triggers = [new AutomationTriggerSpec { Kind = AutomationTriggerKind.Cron, Cron = "* * * * *" }], Target = new AutomationTargetSpec { Repositories = [new AutomationRepository("https://github.com/acme/widgets")] } }, Token).ConfigureAwait(true)).Automation!;
        await _store.UpsertRunAsync(created.Id, new AutomationRunEntry { RunId = "done", Source = AutomationRunSource.Cron, Status = status, StartedAt = _clock.GetUtcNow(), FinishedAt = _clock.GetUtcNow().AddSeconds(3), Error = "why" }, Token).ConfigureAwait(true);
        await _store.UpsertRunAsync(created.Id, new AutomationRunEntry { RunId = "now", Source = AutomationRunSource.Cron, Status = AutomationRunStatus.Running, StartedAt = _clock.GetUtcNow().AddMinutes(1) }, Token).ConfigureAwait(true);

        var info = (await Provider("alice").ListAsync(Token).ConfigureAwait(true)).ShouldNotBeNull().ShouldHaveSingleItem();

        var last = info.LastRun.ShouldNotBeNull();
        last.Outcome.ShouldBe(outcome);
        last.Reason.ShouldBe("why");
        last.RecordedAt.ShouldBe(_clock.GetUtcNow().AddSeconds(3));
    }

    private sealed class FixedUser(string name) : IUserContext
    {
        public string Name { get; } = name;
    }
}
