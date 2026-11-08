using System.Globalization;
using Autonogrammer.Api.Dispatch;
using Autonogrammer.Api.Dispatch.Store;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch.Store;

/// <summary>The sweep still finds an overdue session command while 500 running tasks are in flight, because the store returns only overdue commands.</summary>
public sealed class SweepRegressionTests
{
    /// <summary>500 running tasks entered their state first. The session command entered later and passed its 60-second limit. The sweep moves the session command to failed timeout and leaves the tasks running.</summary>
    [Fact]
    public async Task An_overdue_session_command_is_swept_while_500_running_tasks_are_in_flight()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new SettableClock();
        var store = new InMemoryDispatchStore(new DispatchStoreLimits(1000, 20, TimeSpan.FromDays(1), TimeSpan.FromDays(7)), clock);
        await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "dev", null, "jti", null, [], null), ct).ConfigureAwait(true);

        for (var index = 0; index < 500; index++)
        {
            var taskId = "task-" + index.ToString("D4", CultureInfo.InvariantCulture);
            _ = await store.EnqueueAsync(new EnqueueDispatchCommand("acct", "dev", taskId, "k-" + taskId, "fp", DispatchCapability.Cowork, DispatchOp.CoworkTask, "{}", clock.UtcNow, clock.UtcNow.AddMinutes(5)), ct).ConfigureAwait(true);
            await RunToRunningAsync(store, taskId, ct).ConfigureAwait(true);
        }

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        _ = await store.EnqueueAsync(new EnqueueDispatchCommand("acct", "dev", "session-op", "k-session", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", clock.UtcNow, clock.UtcNow.AddMinutes(5)), ct).ConfigureAwait(true);
        await RunToRunningAsync(store, "session-op", ct).ConfigureAwait(true);

        clock.UtcNow = clock.UtcNow.AddSeconds(61);
        var services = new ServiceCollection();
        services.AddSingleton<IDispatchStore>(store);
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IOptions<DispatchOptions>>(Microsoft.Extensions.Options.Options.Create(new DispatchOptions { Enabled = true }));
        services.AddLogging();
        services.AddSingleton<DispatchSweeper>();
        using var provider = services.BuildServiceProvider();

        var moved = await provider.GetRequiredService<DispatchSweeper>().SweepOnceAsync(ct).ConfigureAwait(true);

        moved.ShouldBe(1);
        var session = await store.GetCommandAsync("acct", "session-op", ct).ConfigureAwait(true);
        session!.State.ShouldBe(DispatchCommandState.Failed);
        session.Reason.ShouldBe(DispatchFailureReason.Timeout);
        (await store.GetCommandAsync("acct", "task-0000", ct).ConfigureAwait(true))!.State.ShouldBe(DispatchCommandState.Running);
        var feed = (await store.ReadFeedAsync("acct", 0, ct).ConfigureAwait(true)).Events;
        feed.Select(item => item.Kind).ShouldBe(["command.state"]);
    }

    private static async Task RunToRunningAsync(InMemoryDispatchStore store, string commandId, CancellationToken cancellationToken)
    {
        (DispatchCommandState From, DispatchCommandState To)[] path =
        [
            (DispatchCommandState.Queued, DispatchCommandState.Delivered),
            (DispatchCommandState.Delivered, DispatchCommandState.Received),
            (DispatchCommandState.Received, DispatchCommandState.Accepted),
            (DispatchCommandState.Accepted, DispatchCommandState.Running),
        ];
        foreach (var (from, to) in path)
        {
            (await store.TryTransitionAsync("acct", commandId, from, to, null, null, cancellationToken).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        }
    }

    private sealed class SettableClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
