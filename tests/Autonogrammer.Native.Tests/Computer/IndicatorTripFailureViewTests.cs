using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Computer;
using Autonogrammer.Presentation.Computer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Computer;

/// <summary>A Stop that fails is said on the indicator, and the sentence goes away once a later Stop succeeds.</summary>
public sealed class IndicatorTripFailureViewTests
{
    [Fact]
    public async Task AFailedStopIsSaidOnTheIndicatorUntilAStopSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var switchControl = new TrippingSwitch();
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new ComputerIndicatorViewModel(switchControl, "session-1", "Ctrl+Alt+Esc");
            var window = new ComputerIndicatorWindow(model);
            window.Show();
            try
            {
                SettingsTree.SettleSafe(window);
                SettingsTree.Texts(window).ShouldNotContain(ComputerIndicatorViewModel.TripFailed, StringComparer.Ordinal);

                switchControl.Fail = true;
                model.Stop.Execute(null);
                SettingsTree.SettleSafe(window);
                SettingsTree.Texts(window).ShouldContain(ComputerIndicatorViewModel.TripFailed, StringComparer.Ordinal);

                switchControl.Fail = false;
                model.Stop.Execute(null);
                SettingsTree.SettleSafe(window);
                SettingsTree.Texts(window).ShouldNotContain(ComputerIndicatorViewModel.TripFailed, StringComparer.Ordinal);
            }
            finally
            {
                window.Close();
                model.Dispose();
            }
        }, ct).ConfigureAwait(true);
    }

    private sealed class TrippingSwitch : IKillSwitchControl
    {
        public bool Fail { get; set; }

        public KillSwitchState State => KillSwitchState.Armed;

        public event EventHandler<KillSwitchStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public ValueTask TripAsync(CancellationToken cancellationToken) =>
            Fail ? ValueTask.FromException(new InvalidOperationException("The switch did not trip.")) : ValueTask.CompletedTask;

        public ValueTask RearmAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
