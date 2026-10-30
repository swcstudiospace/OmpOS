using Autonogrammer.Harness.Automations.Hosted;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Automations.Tests;

/// <summary>
/// Verifies the runner against a scripted target: admission, the concurrency and hourly guards, the maximum runtime, failure recording
/// without leaking exception text, shutdown, and recovery of runs a previous process left running.
/// </summary>
public sealed class AutomationRunnerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_admitted_run_is_recorded_as_running_then_as_launched_with_the_agent_it_created()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make(), Token).ConfigureAwait(true);
        fixture.Target.Hold = true;

        var start = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);

        start.Admitted.ShouldBeTrue();
        await ScriptedTargetWait.UntilAsync(() => fixture.Target.Waiting == 1, Token).ConfigureAwait(true);
        var running = (await fixture.Store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true)).ShouldHaveSingleItem();
        running.Status.ShouldBe(AutomationRunStatus.Running);
        running.FinishedAt.ShouldBeNull();
        running.Source.ShouldBe(AutomationRunSource.Manual);

        fixture.Clock.Advance(TimeSpan.FromSeconds(42));
        fixture.Target.Release(AutomationTargetResult.Launched("agent-77"));
        var final = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);

        final.Status.ShouldBe(AutomationRunStatus.Launched);
        final.ExternalId.ShouldBe("agent-77");
        final.Duration.ShouldBe(TimeSpan.FromSeconds(42));
        var stored = (await fixture.Store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true)).ShouldHaveSingleItem();
        stored.ShouldBe(final);
    }

    [Fact]
    public async Task The_target_receives_the_prompt_the_trigger_summary_the_allowed_tools_and_the_run_id_as_its_key()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make() with { AllowedTools = [AutomationTools.PullRequestCreate, AutomationTools.PullRequestComment] }, Token).ConfigureAwait(true);
        var trigger = new AutomationTriggerContext { Source = AutomationRunSource.Webhook, Summary = "push to main by octocat", Payload = "branch: main" };

        var start = await fixture.Runner.StartAsync(automation, trigger, Token).ConfigureAwait(true);
        _ = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);

        var request = fixture.Target.Requests.ShouldHaveSingleItem();
        request.RunId.ShouldBe(start.RunId);
        request.Trigger.ShouldBe(trigger);
        request.Prompt.ShouldStartWith("Review the changes of the last day.", Case.Sensitive);
        request.Prompt.ShouldContain("push to main by octocat", Case.Sensitive);
        request.Prompt.ShouldContain("pull_request_create, pull_request_comment", Case.Sensitive);
        request.Prompt.ShouldContain("BEGIN UNTRUSTED TRIGGER DATA", Case.Sensitive);
    }

    [Fact]
    public async Task A_disabled_automation_can_still_be_run_by_hand()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make(enabled: false), Token).ConfigureAwait(true);

        var start = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);

        start.Admitted.ShouldBeTrue();
        (await start.Completion!.WaitAsync(Token).ConfigureAwait(true)).Status.ShouldBe(AutomationRunStatus.Launched);
    }

    [Fact]
    public async Task A_failed_result_is_recorded_with_its_error()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make(), Token).ConfigureAwait(true);
        fixture.Target.Respond = static _ => AutomationTargetResult.Failed("The cloud agent was refused (agent_limit).");

        var start = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var final = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);

        final.Status.ShouldBe(AutomationRunStatus.Failed);
        final.Error.ShouldBe("The cloud agent was refused (agent_limit).");
    }

    [Fact]
    public async Task A_result_with_a_status_a_target_may_not_report_is_recorded_as_failed()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make(), Token).ConfigureAwait(true);
        fixture.Target.Respond = static _ => new AutomationTargetResult { Status = AutomationRunStatus.Skipped };

        var start = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var final = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);

        final.Status.ShouldBe(AutomationRunStatus.Failed);
        final.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_target_exception_becomes_a_failed_run_without_the_exception_message()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make(), Token).ConfigureAwait(true);
        fixture.Target.Throw = new InvalidOperationException("token sk-live-9f8e7d leaked in this message");

        var start = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var final = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);

        final.Status.ShouldBe(AutomationRunStatus.Failed);
        final.Error.ShouldNotBeNull().ShouldContain("InvalidOperationException", Case.Sensitive);
        final.Error.ShouldNotContain("sk-live", Case.Sensitive);
        var stored = (await fixture.Store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true)).ShouldHaveSingleItem();
        stored.Error.ShouldNotBeNull().ShouldNotContain("sk-live", Case.Sensitive);
    }

    [Fact]
    public async Task A_long_error_is_cut_to_the_limit()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make(), Token).ConfigureAwait(true);
        fixture.Target.Respond = static _ => AutomationTargetResult.Failed(new string('e', 5_000));

        var start = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var final = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);

        final.Error.ShouldNotBeNull().Length.ShouldBe(AutomationLimits.MaxErrorLength);
    }

    [Fact]
    public async Task A_run_that_exceeds_the_maximum_runtime_is_cancelled_and_recorded_as_timed_out()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make() with { MaxRuntimeMinutes = 10 }, Token).ConfigureAwait(true);
        fixture.Target.Hold = true;
        var start = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        await ScriptedTargetWait.UntilAsync(() => fixture.Target.Waiting == 1, Token).ConfigureAwait(true);

        fixture.Clock.Advance(TimeSpan.FromMinutes(9));
        start.Completion!.WaitAsync(Token).IsCompleted.ShouldBeFalse();
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var final = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);

        final.Status.ShouldBe(AutomationRunStatus.TimedOut);
        final.Error.ShouldNotBeNull().ShouldContain("10 minutes", Case.Sensitive);
        final.Duration.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task A_run_for_a_target_kind_the_host_does_not_have_fails_with_a_clear_error()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var local = TestAutomations.Make() with { Target = new AutomationTargetSpec { Kind = AutomationTargetKind.Local } };
        await fixture.SaveAsync(local, Token).ConfigureAwait(true);

        fixture.Runner.HasTarget(AutomationTargetKind.Local).ShouldBeFalse();
        fixture.Runner.HasTarget(AutomationTargetKind.Cloud).ShouldBeTrue();
        var start = await fixture.Runner.StartAsync(local, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var final = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);

        final.Status.ShouldBe(AutomationRunStatus.Failed);
        final.Error.ShouldBe("No Local target is available on this host.");
    }

    [Fact]
    public async Task One_run_at_a_time_is_the_default_and_a_second_start_is_skipped_and_recorded()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make(), Token).ConfigureAwait(true);
        automation.MaxConcurrentRuns.ShouldBe(1);
        fixture.Target.Hold = true;

        var first = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var second = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Cron(Kit.Start), Token).ConfigureAwait(true);

        first.Admitted.ShouldBeTrue();
        second.Admitted.ShouldBeFalse();
        second.Refusal.ShouldBe(AutomationRunRefusal.ConcurrencyLimit);
        second.Completion.ShouldBeNull();
        var history = await fixture.Store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true);
        history.Count.ShouldBe(2);
        history[0].Status.ShouldBe(AutomationRunStatus.Skipped);
        history[0].Source.ShouldBe(AutomationRunSource.Cron);
        history[0].Error.ShouldNotBeNull().ShouldContain("maximum number of runs", Case.Sensitive);

        await fixture.Target.ReleaseWhenHeldAsync(1, Token).ConfigureAwait(true);
        _ = await first.Completion!.WaitAsync(Token).ConfigureAwait(true);
        var third = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        third.Admitted.ShouldBeTrue();
        await fixture.Target.ReleaseWhenHeldAsync(2, Token).ConfigureAwait(true);
        _ = await third.Completion!.WaitAsync(Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task A_higher_concurrency_limit_admits_that_many_runs_at_once()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make() with { MaxConcurrentRuns = 2 }, Token).ConfigureAwait(true);
        fixture.Target.Hold = true;

        var first = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var second = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var third = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);

        first.Admitted.ShouldBeTrue();
        second.Admitted.ShouldBeTrue();
        third.Refusal.ShouldBe(AutomationRunRefusal.ConcurrencyLimit);
        await fixture.Target.ReleaseWhenHeldAsync(2, Token).ConfigureAwait(true);
        _ = await Task.WhenAll(first.Completion!.WaitAsync(Token), second.Completion!.WaitAsync(Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task The_concurrency_limit_is_per_automation()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var one = await fixture.SaveAsync(TestAutomations.Make(id: "aut_one"), Token).ConfigureAwait(true);
        var two = await fixture.SaveAsync(TestAutomations.Make(id: "aut_two"), Token).ConfigureAwait(true);
        fixture.Target.Hold = true;

        var first = await fixture.Runner.StartAsync(one, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        var second = await fixture.Runner.StartAsync(two, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);

        first.Admitted.ShouldBeTrue();
        second.Admitted.ShouldBeTrue();
        await fixture.Target.ReleaseWhenHeldAsync(2, Token).ConfigureAwait(true);
        _ = await Task.WhenAll(first.Completion!.WaitAsync(Token), second.Completion!.WaitAsync(Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task The_hourly_cap_refuses_a_run_until_the_oldest_run_leaves_the_rolling_hour()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make() with { MaxRunsPerHour = 2 }, Token).ConfigureAwait(true);

        var first = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        _ = await first.Completion!.WaitAsync(Token).ConfigureAwait(true);
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        var second = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        _ = await second.Completion!.WaitAsync(Token).ConfigureAwait(true);

        fixture.Clock.Advance(TimeSpan.FromMinutes(29));
        var refused = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        refused.Refusal.ShouldBe(AutomationRunRefusal.HourlyCap);

        // 60 minutes after the first run it leaves the window; the skipped attempt never counted.
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var admitted = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        admitted.Admitted.ShouldBeTrue();
        _ = await admitted.Completion!.WaitAsync(Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task Refusals_do_not_count_against_the_cap_and_failed_runs_do()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make() with { MaxRunsPerHour = 1 }, Token).ConfigureAwait(true);
        fixture.Target.Respond = static _ => AutomationTargetResult.Failed("nope");
        _ = await (await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true)).Completion!.WaitAsync(Token).ConfigureAwait(true);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true)).Refusal.ShouldBe(AutomationRunRefusal.HourlyCap);
        }

        fixture.Clock.Advance(TimeSpan.FromHours(1));
        (await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true)).Admitted.ShouldBeTrue();
    }

    [Fact]
    public async Task Disposing_the_runner_cancels_the_runs_in_flight_and_records_them_as_cancelled()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make(), Token).ConfigureAwait(true);
        fixture.Target.Hold = true;
        var start = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);
        await ScriptedTargetWait.UntilAsync(() => fixture.Target.Waiting == 1, Token).ConfigureAwait(true);

        await fixture.Runner.DisposeAsync().ConfigureAwait(true);

        var final = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);
        final.Status.ShouldBe(AutomationRunStatus.Cancelled);
        (await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true)).Refusal.ShouldBe(AutomationRunRefusal.ShuttingDown);
    }

    [Fact]
    public async Task Recovery_marks_runs_a_previous_process_left_running_as_failed_and_leaves_live_runs_alone()
    {
        var fixture = new RunnerFixture();
        await using var fixtureScope = fixture.ConfigureAwait(true);
        var automation = await fixture.SaveAsync(TestAutomations.Make() with { MaxConcurrentRuns = 2 }, Token).ConfigureAwait(true);
        await fixture.Store.UpsertRunAsync(
            automation.Id,
            new AutomationRunEntry { RunId = "stale", Source = AutomationRunSource.Cron, Status = AutomationRunStatus.Running, StartedAt = Kit.Start.AddMinutes(-90) },
            Token).ConfigureAwait(true);
        fixture.Target.Hold = true;
        var live = await fixture.Runner.StartAsync(automation, AutomationTriggerContext.Manual(), Token).ConfigureAwait(true);

        var marked = await fixture.Runner.RecoverInterruptedRunsAsync(Token).ConfigureAwait(true);

        marked.ShouldBe(1);
        var history = await fixture.Store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true);
        history.Single(static entry => string.Equals(entry.RunId, "stale", StringComparison.Ordinal)).Status.ShouldBe(AutomationRunStatus.Failed);
        history.Single(static entry => string.Equals(entry.RunId, "stale", StringComparison.Ordinal)).Error.ShouldNotBeNull().ShouldContain("interrupted", Case.Sensitive);
        history.Single(entry => string.Equals(entry.RunId, live.RunId, StringComparison.Ordinal)).Status.ShouldBe(AutomationRunStatus.Running);
        await fixture.Target.ReleaseWhenHeldAsync(1, Token).ConfigureAwait(true);
        _ = await live.Completion!.WaitAsync(Token).ConfigureAwait(true);
    }

    [Fact]
    public void Two_targets_of_one_kind_are_refused()
    {
        Should.Throw<ArgumentException>(() => new AutomationRunner(new InMemoryAutomationStore(), [new ScriptedTarget(), new ScriptedTarget()], new FakeTimeProvider(Kit.Start)));
    }
}
