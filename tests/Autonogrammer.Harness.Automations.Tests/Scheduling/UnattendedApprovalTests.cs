using System.Globalization;

using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Harness.Automations.Scheduling;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Automations.Tests.Scheduling;

/// <summary>Verifies an unattended approval waits, raises waiting_for_approval, and cancels after 30 minutes.</summary>
public sealed class UnattendedApprovalTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void The_unattended_wait_is_30_minutes_and_the_catch_up_window_is_24_hours()
    {
        var options = new Autonogrammer.Harness.Automations.AutomationOptions();
        options.UnattendedApprovalTimeout.ShouldBe(TimeSpan.FromMinutes(30));
        options.CatchUpWindow.ShouldBe(TimeSpan.FromHours(24));
    }

    [Theory]
    [InlineData(SessionApprovalMode.Manual)]
    [InlineData(SessionApprovalMode.Auto)]
    public async Task An_unattended_run_waits_then_is_cancelled_and_nothing_is_approved(SessionApprovalMode mode)
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var store = new InMemoryAutomationStore();
        var starter = new RecordingStarter();
        var runner = new AutomationRunner(store, [], clock, [starter]);
        await using var runnerScope = runner.ConfigureAwait(true);
        var automation = TestAutomations.Make() with
        {
            Target = new AutomationTargetSpec
            {
                Kind = AutomationTargetKind.Session,
                Surface = ScheduledSessionSurface.Cowork,
                ApprovalMode = mode,
                MissedRunPolicy = MissedRunPolicy.RunOnce,
            },
        };
        await store.SaveAsync(automation, Token).ConfigureAwait(true);

        var start = await runner.StartAsync(automation, AutomationTriggerContext.Cron(Kit.Start), Token).ConfigureAwait(true);
        AutomationRunEntry? waiting = null;
        for (var attempt = 0; attempt < 50 && waiting is null; attempt++)
        {
            await Task.Delay(10, Token).ConfigureAwait(true);
            var history = await store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true);
            waiting = history.FirstOrDefault(entry => entry.Status == AutomationRunStatus.WaitingForApproval);
        }

        waiting.ShouldNotBeNull();
        waiting.Summary.ShouldContain("schedule.waiting_for_approval");
        waiting.Summary.ShouldContain("schedule.run");
        waiting.Source.ShouldBe(AutomationRunSource.Cron);
        starter.Calls.ShouldBe(0);

        clock.Advance(TimeSpan.FromMinutes(29));
        starter.Calls.ShouldBe(0);
        clock.Advance(TimeSpan.FromMinutes(1));
        var final = await start.Completion!.WaitAsync(Token).ConfigureAwait(true);

        final.Status.ShouldBe(AutomationRunStatus.Cancelled);
        final.Error.ShouldNotBeNull().ShouldContain("Nothing was approved");
        final.Summary.ShouldContain("schedule.waiting_for_approval");
        final.Summary.ShouldContain(Kit.Start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        starter.Calls.ShouldBe(0);
        final.Status.ShouldNotBe(AutomationRunStatus.Succeeded);
    }

    private sealed class RecordingStarter : IScheduledSessionStarter
    {
        public ScheduledSessionSurface Surface => ScheduledSessionSurface.Cowork;

        public int Calls { get; private set; }

        public Task<ScheduledSessionStart> StartAsync(
            ScheduledSessionSurface surface,
            string? projectId,
            IReadOnlyList<string> trustedFolders,
            SessionApprovalMode approvalMode,
            string? model,
            string prompt,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new ScheduledSessionStart(true, "sess", null));
        }
    }
}
