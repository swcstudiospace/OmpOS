using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Computer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Computer;

/// <summary>Dispatch origin is a label. A remote answer is refused. The timeout denies.</summary>
public sealed class DispatchOriginTests
{
    [Fact]
    public void DispatchDeviceNameAppearsOnThePrompt()
    {
        using var model = new ComputerApprovalViewModel(Facts("Desk"));

        model.OriginLabel.ShouldBe("Dispatch: Desk");
        model.QuotedTitle.ShouldStartWith("\"");
        model.QuotedTitle.Length.ShouldBeLessThanOrEqualTo(ComputerApprovalViewModel.TitleLimit);
    }

    [Fact]
    public void DispatchDeviceNameAppearsOnTheIndicator()
    {
        using var model = new ComputerIndicatorViewModel(new IdleSwitch(), "session-1", "Ctrl+Alt+Esc", ComputerActionOrigin.Dispatch, "Desk");
        model.RemoteLabel.ShouldBe("Remote (Dispatch): Desk");
        model.Headline.ShouldBe(ComputerUseCopy.IndicatorHeadline);
    }

    [Fact]
    public async Task RemoteAnswersAreRefused()
    {
        var model = new ComputerApprovalViewModel(Facts("Desk"), new FrozenTime());

        model.TryApplyRemote(ComputerApprovalChoice.AllowOnce).ShouldBeFalse();
        model.TryApplyRemote(ComputerApprovalChoice.AllowForSession).ShouldBeFalse();
        model.IsCompleted.ShouldBeFalse();

        model.Deny.Execute(null);
        (await model.WaitAsync(CancellationToken.None)).ShouldBe(ComputerApprovalChoice.Deny);
        model.Dispose();
    }

    [Fact]
    public async Task TheTimeoutDeniesAndNeverAllows()
    {
        var time = new ManualClock();
        var model = new ComputerApprovalViewModel(Facts("Desk"), time);
        time.Fire();

        (await model.WaitAsync(CancellationToken.None)).ShouldBe(ComputerApprovalChoice.Deny);
        model.Dispose();
    }

    private static ComputerApprovalFacts Facts(string device) => new()
    {
        ApplicationName = "Notes",
        AppIdentity = "notes",
        SanitizedTitle = "Draft",
        HostActionWords = "click",
        HostSanitizedPreview = "hello",
        Origin = ComputerActionOrigin.Dispatch,
        DeviceName = device,
        SecurityPhrase = "pine",
    };

    private sealed class IdleSwitch : IKillSwitchControl
    {
        public KillSwitchState State => KillSwitchState.Armed;

        public event EventHandler<KillSwitchStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public ValueTask TripAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask RearmAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class FrozenTime : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new IdleTimer();
    }

    private sealed class ManualClock : TimeProvider
    {
        private TimerCallback? _callback;

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
