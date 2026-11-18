using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Computer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Computer;

/// <summary>The indicator's Stop: a Stop that fails is said on the indicator, and the indicator follows the kill switch state afterwards.</summary>
public sealed class ComputerIndicatorViewModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>When Stop throws after the switch has tripped, the indicator says Stop failed and shows the state the switch is in.</summary>
    [Fact]
    public async Task AFailedStopIsSaidAndTheIndicatorFollowsTheSwitch()
    {
        var killSwitch = new FailingSwitch();
        using var model = new ComputerIndicatorViewModel(killSwitch, "session-1", "Ctrl+Alt+Esc");
        model.IsVisible.ShouldBeTrue();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        model.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(ComputerIndicatorViewModel.StateLabel), StringComparison.Ordinal))
            {
                changed.TrySetResult();
            }
        };
        var refreshed = changed.Task;

        model.Stop.Execute(null);

        await refreshed.WaitAsync(Token);
        model.TripFailureText.ShouldBe(ComputerIndicatorViewModel.TripFailed);
        model.IsVisible.ShouldBeFalse();
    }

    /// <summary>A kill switch that records the trip and then fails while it releases input.</summary>
    private sealed class FailingSwitch : IKillSwitchControl
    {
        public KillSwitchState State { get; private set; } = KillSwitchState.Armed;

        public event EventHandler<KillSwitchStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public ValueTask TripAsync(CancellationToken cancellationToken)
        {
            State = KillSwitchState.Tripped;
            return ValueTask.FromException(new InvalidOperationException("input could not be released"));
        }

        public ValueTask RearmAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
