using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Computer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Computer;

/// <summary>Every availability state maps to guidance. The poll stops at five minutes.</summary>
public sealed class PermissionAdvisorTests
{
    [Fact]
    public void EveryAvailabilityStateMapsToAPanel()
    {
        foreach (var state in Enum.GetValues<ComputerAvailability>())
        {
            var panel = ComputerPermissionAdvisor.For(state);
            panel.Title.ShouldNotBeNullOrWhiteSpace(state.ToString());
            panel.MessageId.ShouldBe(ComputerAvailabilityRemediation.For(state));
            (panel.HasDeepLink || !string.IsNullOrWhiteSpace(panel.WhyBlocked)).ShouldBeTrue(state.ToString());
            panel.WhatItAllows.Contains("fallback", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(state.ToString());
        }
    }

    [Fact]
    public void AMissingPermissionNamesThePrivacyPaneAndNoFallback()
    {
        var panel = ComputerPermissionAdvisor.ForPermission(new ComputerPermissionStatus
        {
            Kind = ComputerPermissionKind.ScreenRecording,
            Granted = false,
            MessageId = "computer.permission.screen-recording",
        });

        panel.DeepLink.ShouldBe(ComputerPermissionAdvisor.ScreenRecordingDeepLink);
        panel.WhyBlocked.ShouldNotBeNull();
        panel.WhatItAllows.ShouldContain("No other mechanism");
    }

    [Fact]
    public void ThePollStopsAtFiveMinutes()
    {
        var clock = new ManualClock();
        var advisor = new ComputerPermissionAdvisor(new IdleStatus(), clock);
        advisor.Open();
        advisor.IsPolling.ShouldBeTrue();

        clock.UtcNow += ComputerPermissionAdvisor.PollLimit;
        clock.Fire();

        advisor.IsPolling.ShouldBeFalse();
        advisor.Dispose();
    }

    private sealed class IdleStatus : IComputerStatusSource
    {
        public ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(ComputerAvailability.NoDisplay);

        public ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ComputerPermissionStatus>>([]);

        public ValueTask<ComputerPermissionStatus> RequestPermissionAsync(ComputerPermissionKind kind, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ManualClock : TimeProvider
    {
        private TimerCallback? _callback;

        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => UtcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callback = callback;
            return new IdleTimer();
        }

        public void Fire() => _callback?.Invoke(null);
    }

    private sealed class IdleTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
