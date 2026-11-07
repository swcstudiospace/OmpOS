#pragma warning disable MA0002
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Computer;
using Autonogrammer.Presentation.Computer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Computer;

/// <summary>The indicator is top-most, names the session and the Dispatch device, and does not take focus.</summary>
public sealed class IndicatorWindowViewTests
{
    [Fact]
    public async Task TheIndicatorNamesTheDispatchDeviceAndStaysTopMost()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new ComputerIndicatorViewModel(new ArmedSwitch(), "session-1", "Ctrl+Alt+Esc", ComputerActionOrigin.Dispatch, "Desk");
            var window = new ComputerIndicatorWindow(model);
            window.Show();
            SettingsTree.SettleSafe(window);

            window.Topmost.ShouldBeTrue();
            window.Focusable.ShouldBeFalse();
            SettingsTree.Texts(window).ShouldContain(ComputerUseCopy.IndicatorHeadline);
            SettingsTree.Texts(window).ShouldContain("session-1");
            SettingsTree.Texts(window).ShouldContain("Ctrl+Alt+Esc");
            SettingsTree.Texts(window).ShouldContain("Remote (Dispatch): Desk");
            SettingsTree.Texts(window).ShouldContain(ComputerUseCopy.ArmedIdle);
            window.Close();
            model.Dispose();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private sealed class ArmedSwitch : IKillSwitchControl
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
}

#pragma warning restore MA0002
