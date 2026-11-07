using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Harness.Automations.Scheduling;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Automations.Tests.Scheduling;

/// <summary>Verifies missed-run folding on a manual clock: one late run, a 24 hour cut-off, and no catch-up when disabled.</summary>
public sealed class MissedRunTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task One_overdue_occurrence_starts_exactly_one_late_run_with_both_times_in_the_prompt()
    {
        var created = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
        var due = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
        var harness = await HarnessAsync(created, "0 9 21 10 *").ConfigureAwait(true);
        await using var harnessScope = harness.ConfigureAwait(true);
        harness.Clock.Advance(TimeSpan.FromHours(2));

        (await harness.TickAsync().ConfigureAwait(true)).ShouldBe(1);
        await harness.SettleAsync().ConfigureAwait(true);

        harness.Starter.Calls.Count.ShouldBe(1);
        harness.Starter.Calls[0].Prompt.ShouldContain("Scheduled time (host): 2026-10-21 09:00 UTC");
        harness.Starter.Calls[0].Prompt.ShouldContain("Actual time (host): 2026-10-21 10:00 UTC");
        var entry = await harness.LatestAsync().ConfigureAwait(true);
        entry.Status.ShouldBe(AutomationRunStatus.Launched);
        entry.Source.ShouldBe(AutomationRunSource.Cron);
        entry.Summary.ShouldContain("late");
        entry.Summary.ShouldContain("schedule.run");
        entry.Summary.ShouldContain("schedule.late");
        entry.Summary.ShouldContain(due.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        entry.Summary.ShouldContain("2026-10-21 10:00");
    }

    [Fact]
    public async Task Several_missed_occurrences_fold_into_one_run_and_a_run_never_starts_early()
    {
        var created = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
        var harness = await HarnessAsync(created, "0 * * * *").ConfigureAwait(true);
        await using var harnessScope = harness.ConfigureAwait(true);
        _ = await harness.TickAsync().ConfigureAwait(true);

        harness.Clock.Advance(TimeSpan.FromMinutes(59));
        (await harness.TickAsync().ConfigureAwait(true)).ShouldBe(0);
        harness.Starter.Calls.ShouldBeEmpty();

        harness.Clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(10));
        (await harness.TickAsync().ConfigureAwait(true)).ShouldBe(1);
        await harness.SettleAsync().ConfigureAwait(true);

        harness.Starter.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public async Task An_occurrence_older_than_24_hours_is_skipped()
    {
        var created = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
        var harness = await HarnessAsync(created, "0 9 21 10 *").ConfigureAwait(true);
        await using var harnessScope = harness.ConfigureAwait(true);
        _ = await harness.TickAsync().ConfigureAwait(true);
        harness.Clock.Advance(TimeSpan.FromHours(26));

        (await harness.TickAsync().ConfigureAwait(true)).ShouldBe(0);
        await harness.SettleAsync().ConfigureAwait(true);

        harness.Starter.Calls.ShouldBeEmpty();
        var entry = await harness.LatestAsync().ConfigureAwait(true);
        entry.Status.ShouldBe(AutomationRunStatus.Skipped);
        entry.Error.ShouldNotBeNull().ShouldContain("24 hour");
        entry.Error.ShouldContain("2026-10-21 09:00 UTC");
    }

    [Fact]
    public async Task A_disabled_task_never_catches_up()
    {
        var created = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
        var harness = await HarnessAsync(created, "0 9 21 10 *", enabled: false).ConfigureAwait(true);
        await using var harnessScope = harness.ConfigureAwait(true);
        _ = await harness.TickAsync().ConfigureAwait(true);
        harness.Clock.Advance(TimeSpan.FromHours(2));
        (await harness.TickAsync().ConfigureAwait(true)).ShouldBe(0);

        var enabled = harness.Automation with { Enabled = true };
        await harness.Store.SaveAsync(enabled, Token).ConfigureAwait(true);
        (await harness.TickAsync().ConfigureAwait(true)).ShouldBe(0);

        harness.Starter.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Skip_keeps_todays_five_minute_rule()
    {
        var created = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
        var harness = await HarnessAsync(created, "0 9 21 10 *", policy: MissedRunPolicy.Skip).ConfigureAwait(true);
        await using var harnessScope = harness.ConfigureAwait(true);
        _ = await harness.TickAsync().ConfigureAwait(true);
        harness.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        (await harness.TickAsync().ConfigureAwait(true)).ShouldBe(0);

        harness.Starter.Calls.ShouldBeEmpty();
        var entry = await harness.LatestAsync().ConfigureAwait(true);
        entry.Status.ShouldBe(AutomationRunStatus.Skipped);
        entry.Error.ShouldNotBeNull().ShouldContain("more than 5 minutes late");
    }

    private static async Task<Harness> HarnessAsync(
        DateTimeOffset created,
        string cron,
        bool enabled = true,
        MissedRunPolicy policy = MissedRunPolicy.RunOnce)
    {
        var clock = new FakeTimeProvider(created);
        var store = new InMemoryAutomationStore();
        var starter = new RecordingStarter();
#pragma warning disable CA2000 // The returned harness disposes the runner.
        AutomationRunner runner = new(store, [], clock, [starter], new FixedIsolation(SessionIsolationLevel.FilesystemIsolated));
#pragma warning restore CA2000
        var automation = TestAutomations.Make([TestAutomations.Cron(cron)], enabled: enabled) with
        {
            CreatedAt = created,
            UpdatedAt = created,
            Target = new AutomationTargetSpec
            {
                Kind = AutomationTargetKind.Session,
                Surface = ScheduledSessionSurface.Cowork,
                ApprovalMode = SessionApprovalMode.Skip,
                Preset = SchedulePreset.Daily,
                MissedRunPolicy = policy,
            },
        };
        await store.SaveAsync(automation, Token).ConfigureAwait(false);
        return new Harness(clock, store, starter, runner, new AutomationScheduler(store, runner, clock), automation);
    }

    private sealed class Harness(
        FakeTimeProvider clock,
        InMemoryAutomationStore store,
        RecordingStarter starter,
        AutomationRunner runner,
        AutomationScheduler scheduler,
        UserAutomation automation) : IAsyncDisposable
    {
        public FakeTimeProvider Clock { get; } = clock;

        public InMemoryAutomationStore Store { get; } = store;

        public RecordingStarter Starter { get; } = starter;

        public UserAutomation Automation { get; } = automation;

        public Task<int> TickAsync() => scheduler.TickAsync(Token);

        public async Task SettleAsync()
        {
            while ((await Store.GetHistoryAsync(Automation.Id, Token).ConfigureAwait(false)).Any(static entry => entry.Status == AutomationRunStatus.Running))
            {
                await Task.Yield();
            }
        }

        public async Task<AutomationRunEntry> LatestAsync()
        {
            var history = await Store.GetHistoryAsync(Automation.Id, Token).ConfigureAwait(false);
            return history.ShouldHaveSingleItem();
        }

        public ValueTask DisposeAsync() => runner.DisposeAsync();
    }

    private sealed class FixedIsolation(SessionIsolationLevel level) : ISessionIsolationLevel
    {
        public SessionIsolationLevel Level => level;
    }

    private sealed class RecordingStarter : IScheduledSessionStarter
    {
        public ScheduledSessionSurface Surface => ScheduledSessionSurface.Cowork;

        public List<Call> Calls { get; } = [];

        public Task<ScheduledSessionStart> StartAsync(
            ScheduledSessionSurface surface,
            string? projectId,
            IReadOnlyList<string> trustedFolders,
            SessionApprovalMode approvalMode,
            string? model,
            string prompt,
            CancellationToken cancellationToken)
        {
            Calls.Add(new Call(prompt));
            return Task.FromResult(new ScheduledSessionStart(true, "sess", null));
        }
    }

    private sealed record Call(string Prompt);
}
