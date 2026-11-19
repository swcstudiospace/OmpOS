using System.Reflection;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Tests.Parity;
using Autonogrammer.Native.Views.AgentPanel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>
/// Closing a running agent from the rail asks first when that agent is not the selected one: the rail hands the close over with the row's
/// session, and the shell selects that session and asks through its own confirmation bar, as it does for the selected agent.
/// </summary>
public sealed class UnselectedRunningCloseTests
{
    private static readonly ScriptedTurn Working = new([new TextDeltaEvent("working")], HoldUntilCancelled: true);

    /// <summary>A running agent that is not selected is not closed by the rail's close request; its confirmation is shown first.</summary>
    [Fact]
    public async Task ClosingARunningAgentThatIsNotSelectedAsksFirst()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working));
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Agent two", script: SessionScript.WithTurns(Working));
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            fixture.Panel.SelectSession("session-2");
            ShellHarness.Settle(window);
            await SendAsync(window, "long task").ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => fixture.Runtime("session-2").ViewModel.RequiresCloseConfirmation, "the second agent to run", token).ConfigureAwait(true);
            fixture.Panel.SelectSession("session-1");
            ShellHarness.Settle(window);
            fixture.Panel.Selected?.Id.ShouldBe("session-1");

            var rail = main.GetVisualDescendants().OfType<AgentRailView>().Single();
            var field = typeof(AgentRailView).GetField("CloseRequested", BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
            var closeRequested = field.GetValue(rail).ShouldNotBeNull() as EventHandler<AgentRowEventArgs>;
            closeRequested.ShouldNotBeNull().Invoke(rail, new AgentRowEventArgs("session-2"));

            await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "session-2", StringComparison.Ordinal), "the running agent to be selected for its confirmation", token).ConfigureAwait(true);
            ShellTree.NamedVisible<Border>(main, "CloseConfirmBar").ShouldNotBeNull("closing a running agent that is not selected asks first");
            fixture.Host.Closed.ShouldBeEmpty();

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            ShellTree.NamedVisible<Border>(main, "CloseConfirmBar").ShouldBeNull("Esc keeps the running agent open");
            fixture.Host.Closed.ShouldBeEmpty("keeping the agent open stops nothing");

            var runtime = fixture.Runtime("session-2");
            await runtime.Composer.StopAsync().ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static async Task SendAsync(MainWindow window, string text)
    {
        ShellHarness.Prompt(window).Text = text;
        ShellHarness.Prompt(window).Focus();
        ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);
    }
}
