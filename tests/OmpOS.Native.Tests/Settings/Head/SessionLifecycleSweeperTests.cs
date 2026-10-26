using OmpOS.Contracts.Agents;
using OmpOS.Native.Agents;
using OmpOS.Native.Settings;
using OmpOS.Native.Tests.AgentLoop;
using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>The Runtime settings applied to real sessions of a head: idle stop and retention, on a clock the test moves.</summary>
public sealed class SessionLifecycleSweeperTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A session whose turn is running is never stopped or removed, however long it has been quiet.</summary>
    [Fact]
    public async Task ARunningSessionIsNeverTouched()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var clock = new ManualTimeProvider(Start);
        var head = AgentHead.Create(Options(tree, clock));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var id = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        head.Host.Registry!.SetLifecycle(id, SessionLifecycle.Running).ShouldBeTrue();

        clock.Advance(TimeSpan.FromDays(30));
        var outcome = await head.Settings.Sweeper!.SweepAsync(ct).ConfigureAwait(true);

        outcome.ShouldBe(SweepOutcome.None);
        head.Host.IsClosed(id).ShouldBeFalse();
        head.Host.Registry.TryGet(id, out var record).ShouldBeTrue();
        record!.Lifecycle.ShouldBe(SessionLifecycle.Running);
    }

    /// <summary>An idle session sleeps once it has been quiet for the idle stop window and not before: its engine is stopped and it can be reopened.</summary>
    [Fact]
    public async Task AnIdleSessionSleepsAfterTheIdleStopWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var clock = new ManualTimeProvider(Start);
        var head = AgentHead.Create(Options(tree, clock));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var id = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        var sweeper = head.Settings.Sweeper.ShouldNotBeNull();
        sweeper.Current.ShouldBe(RuntimeSettingsValues.Defaults);

        clock.Advance(TimeSpan.FromHours(23));
        (await sweeper.SweepAsync(ct).ConfigureAwait(true)).ShouldBe(SweepOutcome.None);
        head.Host.IsClosed(id).ShouldBeFalse();

        clock.Advance(TimeSpan.FromHours(2));
        (await sweeper.SweepAsync(ct).ConfigureAwait(true)).ShouldBe(new SweepOutcome(Slept: 1, Removed: 0));

        head.Host.IsClosed(id).ShouldBeTrue();
        head.Host.Registry!.TryGet(id, out var record).ShouldBeTrue();
        record!.Lifecycle.ShouldBe(SessionLifecycle.Paused);
        (await sweeper.SweepAsync(ct).ConfigureAwait(true)).ShouldBe(SweepOutcome.None);
    }

    /// <summary>A completed session is removed from the registry once it has been quiet for the retention window and not before.</summary>
    [Fact]
    public async Task ACompletedSessionIsRemovedAfterTheRetentionWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var clock = new ManualTimeProvider(Start);
        var head = AgentHead.Create(Options(tree, clock));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var id = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        head.Host.Registry!.SetLifecycle(id, SessionLifecycle.Completed).ShouldBeTrue();
        var sweeper = head.Settings.Sweeper.ShouldNotBeNull();

        clock.Advance(TimeSpan.FromDays(6));
        (await sweeper.SweepAsync(ct).ConfigureAwait(true)).ShouldBe(new SweepOutcome(Slept: 1, Removed: 0));
        head.Host.Registry.TryGet(id, out var kept).ShouldBeTrue();
        kept!.Lifecycle.ShouldBe(SessionLifecycle.Completed);

        clock.Advance(TimeSpan.FromDays(2));
        (await sweeper.SweepAsync(ct).ConfigureAwait(true)).ShouldBe(new SweepOutcome(Slept: 0, Removed: 1));
        head.Host.Registry.TryGet(id, out _).ShouldBeFalse();
    }

    /// <summary>Removing a completed session also deletes that session's own state directory, and no other session's.</summary>
    [Fact]
    public async Task RemovingACompletedSessionDeletesItsOwnStateDirectoryOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var clock = new ManualTimeProvider(Start);
        var head = AgentHead.Create(Options(tree, clock));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var finished = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        var other = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        var registry = head.Host.Registry!;
        registry.TryGet(finished, out var finishedRecord).ShouldBeTrue();
        registry.TryGet(other, out var otherRecord).ShouldBeTrue();
        var finishedFile = Path.Combine(finishedRecord!.StateDirectory, "ultrathink", "state.json");
        var otherFile = Path.Combine(otherRecord!.StateDirectory, "ultrathink", "state.json");
        foreach (var file in new[] { finishedFile, otherFile })
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, "{}", ct).ConfigureAwait(true);
        }

        registry.SetLifecycle(finished, SessionLifecycle.Completed).ShouldBeTrue();
        clock.Advance(TimeSpan.FromDays(8));
        var outcome = await head.Settings.Sweeper!.SweepAsync(ct).ConfigureAwait(true);

        outcome.Removed.ShouldBe(1);
        registry.TryGet(finished, out _).ShouldBeFalse();
        Directory.Exists(finishedRecord.StateDirectory).ShouldBeFalse("the removed session's own directory is deleted with it");
        File.Exists(otherFile).ShouldBeTrue("another session's state is untouched");
    }

    /// <summary>A session that is not completed outlives the retention window: only completed sessions are removed.</summary>
    [Fact]
    public async Task OnlyCompletedSessionsAreRemoved()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var clock = new ManualTimeProvider(Start);
        var head = AgentHead.Create(Options(tree, clock));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var failed = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        var created = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        head.Host.Registry!.SetLifecycle(failed, SessionLifecycle.Failed).ShouldBeTrue();

        clock.Advance(TimeSpan.FromDays(60));
        _ = await head.Settings.Sweeper!.SweepAsync(ct).ConfigureAwait(true);

        head.Host.Registry.TryGet(failed, out _).ShouldBeTrue();
        head.Host.Registry.TryGet(created, out _).ShouldBeTrue();
    }

    /// <summary>New windows take effect at once on sessions that are open now: a shorter idle stop puts a session to sleep that the old window kept awake.</summary>
    [Fact]
    public async Task AppliedValuesTakeEffectOnTheSessionsThatAreOpen()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var clock = new ManualTimeProvider(Start);
        var head = AgentHead.Create(Options(tree, clock));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var id = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        var sweeper = head.Settings.Sweeper.ShouldNotBeNull();
        clock.Advance(TimeSpan.FromHours(3));
        (await sweeper.SweepAsync(ct).ConfigureAwait(true)).ShouldBe(SweepOutcome.None);

        sweeper.Apply(new RuntimeSettingsValues(IdleStopHours: 2, RetentionDays: 7));

        sweeper.Current.IdleStopHours.ShouldBe(2);
        head.Host.IsClosed(id).ShouldBeTrue();
        head.Host.Registry!.TryGet(id, out var record).ShouldBeTrue();
        record!.Lifecycle.ShouldBe(SessionLifecycle.Paused);
    }

    /// <summary>The head reads the saved windows at start, so a value saved in an earlier run is in force from the first pass.</summary>
    [Fact]
    public async Task TheHeadStartsWithTheWindowsTheFileHolds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        new FileSettingsStore(tree.State).SetRuntime(new RuntimeSettingsValues(IdleStopHours: 5, RetentionDays: 2));
        var head = AgentHead.Create(Options(tree, new ManualTimeProvider(Start)));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);

        head.Settings.Sweeper.ShouldNotBeNull().Current.ShouldBe(new RuntimeSettingsValues(5, 2));
    }

    /// <summary>Starting makes a first pass at once, and the timer makes the next ones when it fires.</summary>
    [Fact]
    public async Task StartMakesAFirstPassAndTheTimerRepeatsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var clock = new ManualTimeProvider(Start);
        var head = AgentHead.Create(Options(tree, clock, sweep: TimeSpan.FromMinutes(5)));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var timer = clock.Timers.Single(static candidate => candidate.Period == TimeSpan.FromMinutes(5));
        var id = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        head.Host.IsClosed(id).ShouldBeFalse();

        clock.Advance(TimeSpan.FromHours(30));
        timer.Fire();

        head.Host.IsClosed(id).ShouldBeTrue();
    }

    /// <summary>A head built without a sweep interval runs no sweeper, so a test head has no timer.</summary>
    [Fact]
    public async Task AHeadWithoutASweepIntervalRunsNoSweeper()
    {
        using var tree = new TempTree();
        var options = new AgentHeadOptions
        {
            PluginsRoot = tree.Plugins,
            StateRoot = tree.State,
            WorkspaceRoot = tree.Workspace,
            SelectMostRecentSession = false,
            MaintenanceInterval = Timeout.InfiniteTimeSpan,
            DetailsRefreshInterval = Timeout.InfiniteTimeSpan,
        };
        var head = AgentHead.Create(options);
        await using var headLifetime = head.ConfigureAwait(true);

        head.Settings.Sweeper.ShouldBeNull();
    }

    private static AgentHeadOptions Options(TempTree tree, ManualTimeProvider clock, TimeSpan? sweep = null) => new()
    {
        PluginsRoot = tree.Plugins,
        StateRoot = tree.State,
        WorkspaceRoot = tree.Workspace,
        TimeProvider = clock,
        SelectMostRecentSession = false,
        MaintenanceInterval = Timeout.InfiniteTimeSpan,
        DetailsRefreshInterval = Timeout.InfiniteTimeSpan,
        RuntimeSweepInterval = sweep ?? Timeout.InfiniteTimeSpan,
    };
}
