using OmpOS.Harness.Automations.Hosted;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Verifies the cron scheduler on a fake clock, with no sleeping: runs start at their occurrence and never before, a late occurrence runs
/// once within the misfire window and is skipped beyond it, missed occurrences fold into one, and a disabled automation does not catch up.
/// </summary>
public sealed class AutomationSchedulerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<AutomationScheduler> CreateAsync(RunnerFixture fixture, UserAutomation automation)
    {
        await fixture.SaveAsync(automation, Token).ConfigureAwait(false);
        return new AutomationScheduler(fixture.Store, fixture.Runner, fixture.Clock);
    }

    private static async Task<int> TickAndSettleAsync(AutomationScheduler scheduler, RunnerFixture fixture, string id)
    {
        var started = await scheduler.TickAsync(Token).ConfigureAwait(false);
        while ((await fixture.Store.GetHistoryAsync(id, Token).ConfigureAwait(false)).Any(static entry => entry.Status == AutomationRunStatus.Running))
        {
            await Task.Yield();
        }

        return started;
    }

    [Fact]
    public async Task A_run_starts_at_its_occurrence_with_that_occurrence_in_its_context()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var scheduler = await CreateAsync(fixture, TestAutomations.Make()).ConfigureAwait(true);
        (await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true)).ShouldBe(0);

        fixture.Clock.Advance(TimeSpan.FromDays(1));
        var started = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        started.ShouldBe(1);
        var request = fixture.Target.Requests.ShouldHaveSingleItem();
        request.Trigger.Source.ShouldBe(AutomationRunSource.Cron);
        request.Trigger.ScheduledFor.ShouldBe(new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task A_run_never_starts_before_its_occurrence()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var scheduler = await CreateAsync(fixture, TestAutomations.Make()).ConfigureAwait(true);
        _ = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        fixture.Clock.Advance(TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1));
        (await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true)).ShouldBe(0);
        fixture.Target.Requests.ShouldBeEmpty();

        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        (await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true)).ShouldBe(1);
    }

    [Fact]
    public async Task An_occurrence_is_handled_once_however_often_the_scheduler_looks()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var scheduler = await CreateAsync(fixture, TestAutomations.Make()).ConfigureAwait(true);
        _ = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);
        fixture.Clock.Advance(TimeSpan.FromDays(1));

        _ = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);
        _ = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        _ = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        fixture.Target.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_late_occurrence_runs_once_within_five_minutes_and_is_skipped_after()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var scheduler = await CreateAsync(fixture, TestAutomations.Make()).ConfigureAwait(true);
        _ = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        // Occurrence of 2026-10-23 09:00, observed exactly 5 minutes late: still inside the window.
        fixture.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(5));
        (await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true)).ShouldBe(1);

        // Occurrence of 2026-10-24 09:00, observed 5 minutes and 1 second late: skipped and recorded.
        fixture.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));
        (await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true)).ShouldBe(0);

        fixture.Target.Requests.Count.ShouldBe(1);
        var history = await fixture.Store.GetHistoryAsync("aut_0001", Token).ConfigureAwait(true);
        history[0].Status.ShouldBe(AutomationRunStatus.Skipped);
        history[0].Source.ShouldBe(AutomationRunSource.Cron);
        history[0].Error.ShouldNotBeNull().ShouldContain("more than 5 minutes late", Case.Sensitive);
        history[0].Error.ShouldNotBeNull().ShouldContain("2026-10-24 09:00 UTC", Case.Sensitive);
    }

    [Fact]
    public async Task Several_missed_occurrences_fold_into_a_single_run_when_the_latest_is_inside_the_window()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var scheduler = await CreateAsync(fixture, TestAutomations.Make([TestAutomations.Cron("*/10 * * * *")])).ConfigureAwait(true);
        _ = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        fixture.Clock.Advance(TimeSpan.FromMinutes(63));
        var started = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        started.ShouldBe(1);
        fixture.Target.Requests.ShouldHaveSingleItem().Trigger.ScheduledFor.ShouldBe(new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Several_missed_occurrences_beyond_the_window_are_one_skipped_entry_not_many()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var scheduler = await CreateAsync(fixture, TestAutomations.Make([TestAutomations.Cron("*/10 * * * *")])).ConfigureAwait(true);
        _ = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        fixture.Clock.Advance(TimeSpan.FromMinutes(57));
        (await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true)).ShouldBe(0);

        fixture.Target.Requests.ShouldBeEmpty();
        (await fixture.Store.GetHistoryAsync("aut_0001", Token).ConfigureAwait(true)).Count(static entry => entry.Status == AutomationRunStatus.Skipped).ShouldBe(1);
    }

    [Fact]
    public async Task A_disabled_automation_does_not_run_and_does_not_catch_up_when_it_is_enabled()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var disabled = TestAutomations.Make([TestAutomations.Cron("* * * * *")], enabled: false);
        var scheduler = await CreateAsync(fixture, disabled).ConfigureAwait(true);
        _ = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        fixture.Clock.Advance(TimeSpan.FromDays(2));
        (await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true)).ShouldBe(0);
        await fixture.Store.SaveAsync(disabled with { Enabled = true }, Token).ConfigureAwait(true);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var started = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        started.ShouldBe(1);
        fixture.Target.Requests.Count.ShouldBe(1);
        (await fixture.Store.GetHistoryAsync("aut_0001", Token).ConfigureAwait(true)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Two_triggers_due_at_the_same_instant_start_one_run()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = TestAutomations.Make([TestAutomations.Cron("0 9 * * *"), TestAutomations.Cron("0 9 * * 1-5"), TestAutomations.Cron("0 */3 * * *")]);
        var scheduler = await CreateAsync(fixture, automation with { MaxConcurrentRuns = 4 }).ConfigureAwait(true);
        _ = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        fixture.Clock.Advance(TimeSpan.FromDays(1));
        var started = await TickAndSettleAsync(scheduler, fixture, "aut_0001").ConfigureAwait(true);

        started.ShouldBe(1);
        fixture.Target.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_second_occurrence_while_the_first_run_is_still_going_is_skipped_by_the_concurrency_guard()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var scheduler = await CreateAsync(fixture, TestAutomations.Make([TestAutomations.Cron("* * * * *")])).ConfigureAwait(true);
        fixture.Target.Hold = true;
        _ = await scheduler.TickAsync(Token).ConfigureAwait(true);

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        (await scheduler.TickAsync(Token).ConfigureAwait(true)).ShouldBe(1);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        (await scheduler.TickAsync(Token).ConfigureAwait(true)).ShouldBe(0);

        var history = await fixture.Store.GetHistoryAsync("aut_0001", Token).ConfigureAwait(true);
        history[0].Status.ShouldBe(AutomationRunStatus.Skipped);
        await fixture.Target.ReleaseWhenHeldAsync(1, Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task A_restart_neither_repeats_a_handled_occurrence_nor_loses_one_inside_the_window()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var first = await CreateAsync(fixture, TestAutomations.Make()).ConfigureAwait(true);
        _ = await TickAndSettleAsync(first, fixture, "aut_0001").ConfigureAwait(true);
        fixture.Clock.Advance(TimeSpan.FromDays(1));
        _ = await TickAndSettleAsync(first, fixture, "aut_0001").ConfigureAwait(true);

        var afterRestart = new AutomationScheduler(fixture.Store, fixture.Runner, fixture.Clock);
        (await TickAndSettleAsync(afterRestart, fixture, "aut_0001").ConfigureAwait(true)).ShouldBe(0);

        // The next occurrence comes due while no scheduler is running; the new one finds it 3 minutes late.
        fixture.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(3));
        (await TickAndSettleAsync(afterRestart, fixture, "aut_0001").ConfigureAwait(true)).ShouldBe(1);
        fixture.Target.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task An_automation_whose_zone_or_expression_is_unusable_is_left_alone()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var badZone = TestAutomations.Make([TestAutomations.Cron("* * * * *")], id: "aut_zone", timeZone: "Mars/Olympus");
        var badCron = TestAutomations.Make([TestAutomations.Cron("not a cron")], id: "aut_cron");
        var scheduler = await CreateAsync(fixture, badZone).ConfigureAwait(true);
        await fixture.Store.SaveAsync(badCron, Token).ConfigureAwait(true);
        _ = await scheduler.TickAsync(Token).ConfigureAwait(true);

        fixture.Clock.Advance(TimeSpan.FromHours(3));

        (await scheduler.TickAsync(Token).ConfigureAwait(true)).ShouldBe(0);
        fixture.Target.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_webhook_only_automation_is_never_started_by_the_scheduler()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var scheduler = await CreateAsync(fixture, TestAutomations.Make([TestAutomations.GenericHook()])).ConfigureAwait(true);
        _ = await scheduler.TickAsync(Token).ConfigureAwait(true);

        fixture.Clock.Advance(TimeSpan.FromDays(30));

        (await scheduler.TickAsync(Token).ConfigureAwait(true)).ShouldBe(0);
    }

    [Fact]
    public async Task The_loop_ends_without_an_exception_when_it_is_cancelled()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var scheduler = new AutomationScheduler(fixture.Store, fixture.Runner, fixture.Clock);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await source.CancelAsync().ConfigureAwait(true);

        await scheduler.RunAsync(source.Token).ConfigureAwait(true);
    }

    [Fact]
    public void The_misfire_window_is_five_minutes()
    {
        AutomationLimits.MisfireWindow.ShouldBe(TimeSpan.FromMinutes(5));
    }
}
