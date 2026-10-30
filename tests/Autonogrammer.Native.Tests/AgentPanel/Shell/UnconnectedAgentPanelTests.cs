using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.RunControls;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>The panel a head with no engine builds: one selected session whose every send is refused, and no process of any kind.</summary>
public sealed class UnconnectedAgentPanelTests
{
    /// <summary>The panel is ready with exactly one session, which is selected and carries the motion setting of the head.</summary>
    [Fact]
    public async Task ThePanelHoldsOneSelectedSessionThatCarriesTheMotionSetting()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(() =>
        {
            using var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), null, MotionPreference.Reduced);

            panel.State.ShouldBe(PanelState.Ready);
            panel.SessionCount.ShouldBe(1);
            var selected = panel.Selected.ShouldNotBeNull();
            selected.Id.ShouldBe(UnconnectedAgentPanel.SessionId);
            AgentSessionRuntime.For(selected).ShouldNotBeNull().Motion.ShouldBe(MotionPreference.Reduced);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A send is refused with the status line text, and no run starts.</summary>
    [Fact]
    public async Task ASendIsRefusedAndStartsNoRun()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            using var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), null, MotionPreference.System);
            var runtime = AgentSessionRuntime.For(panel.Selected.ShouldNotBeNull()).ShouldNotBeNull();

            (await runtime.Composer.SendFromAsync("hello").ConfigureAwait(true)).ShouldBeFalse();

            runtime.StatusText.ShouldBe(MainView.RefusedSendStatus);
            runtime.Composer.IsStreaming.ShouldBeFalse();
            runtime.Runs.IsRunActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>New agent fails visibly with the reason that no engine is connected, and adds no session.</summary>
    [Fact]
    public async Task NewAgentSaysThatNoEngineIsConnected()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            using var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), null, MotionPreference.System);

            await panel.NewSessionCommand.ExecuteAsync(null).ConfigureAwait(true);

            panel.ActionError.ShouldBe(AgentPanelCopy.ActionFailed("start a new agent", UnconnectedPromptChannel.UnavailableText));
            panel.SessionCount.ShouldBe(1);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The main view over the panel refuses a typed message on its status line, which is the Android head's whole send path.</summary>
    [Fact]
    public async Task TheMainViewOverThePanelRefusesATypedMessage()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            using var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), null, MotionPreference.System);
            var window = new MainWindow(panel, MotionPreference.System) { Width = 1280, Height = 800 };
            try
            {
                window.Show();
                ShellHarness.Settle(window);

                ShellHarness.Prompt(window).Text = "hello";
                RunControlsHarness.Click(ShellHarness.Send(window));
                await ShellHarness.DrainAsync(window).ConfigureAwait(true);

                ShellHarness.Status(window).Text.ShouldBe(MainView.RefusedSendStatus);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
