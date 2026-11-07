#pragma warning disable MA0002
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Computer;
using Autonogrammer.Presentation.Computer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Computer;

/// <summary>The approval dialog focuses Deny and shows the Dispatch device name.</summary>
public sealed class ApprovalDialogViewTests
{
    [Fact]
    public async Task DenyHasDefaultFocusAndTheDispatchDeviceIsShown()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new ComputerApprovalViewModel(new ComputerApprovalFacts
            {
                ApplicationName = "Notes",
                AppIdentity = "notes",
                SanitizedTitle = "Draft",
                HostActionWords = "click",
                HostSanitizedPreview = "hello",
                Origin = ComputerActionOrigin.Dispatch,
                DeviceName = "Desk",
                SecurityPhrase = "pine",
            }, new QuietClock());
            var window = new ComputerApprovalDialog(model);
            window.Show();
            SettingsTree.SettleSafe(window);

            window.Deny.IsFocused.ShouldBeTrue();
            SettingsTree.Texts(window).ShouldContain("Dispatch: Desk");
            SettingsTree.Texts(window).ShouldContain(ComputerUseCopy.AgentTextLabel);
            window.Close();
            model.Dispose();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private sealed class QuietClock : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new IdleTimer();
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

#pragma warning restore MA0002
