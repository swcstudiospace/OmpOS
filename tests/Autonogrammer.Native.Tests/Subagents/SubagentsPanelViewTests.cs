using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Subagents;
using Autonogrammer.Presentation.Subagents;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Subagents;

/// <summary>The headless sub-agent panel: accessible names, a status live region, the ACP-only notice and an injected clock.</summary>
public sealed class SubagentsPanelViewTests
{
    /// <summary>Rows have accessible names, status changes reach the live region, and the ACP-only notice is on the screen.</summary>
    [Fact]
    public async Task RowsAreNamedAndTheLiveRegionAnnouncesStatus()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SubagentPanelModel(new ManualTimeProvider());
            model.Apply(new SubagentActivityUpdateEventArgs("call-1", "researcher", SubagentStatus.Running, "Searching the repo."));
            var view = new SubagentsPanelView { DataContext = model };
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);

            view.AcpOnlyNotice.Text.ShouldBe(SubagentCopy.AcpOnlyNotice);
            view.AcpOnlyNotice.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(view).ShouldBe(SubagentCopy.PanelName);
            AutomationProperties.GetLiveSetting(view.StatusLiveRegion).ShouldBe(AutomationLiveSetting.Polite);
            view.RunningCountText.Text.ShouldBe("1 running");
            view.StatusLiveRegion.Text.ShouldBe("researcher running");

            var row = view.RowList.GetRealizedContainers().ShouldHaveSingleItem().ShouldBeOfType<ContentPresenter>();
            var border = row.GetVisualChildren().ShouldHaveSingleItem().ShouldBeOfType<Border>();
            AutomationProperties.GetName(border).ShouldBe("researcher, running, 0:00");

            model.Apply(new SubagentActivityUpdateEventArgs("call-1", null, SubagentStatus.Done, "done"));
            SettingsTree.SettleSafe(window);
            view.StatusLiveRegion.Text.ShouldBe("researcher done");
            view.RunningCountText.Text.ShouldBe("0 running");
            AutomationProperties.GetName(border).ShouldBe("researcher, done, 0:00");
            window.Close();
        }, token).ConfigureAwait(true);
    }

    /// <summary>Elapsed text moves only when the injected clock advances. This test does not sleep.</summary>
    [Fact]
    public async Task ElapsedTimeFollowsTheInjectedClock()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var model = new SubagentPanelModel(clock);
            model.Apply(new SubagentActivityUpdateEventArgs("call-1", null, SubagentStatus.Running, "working"));
            var view = new SubagentsPanelView { DataContext = model };
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);

            model.Rows[0].ElapsedText.ShouldBe("0:00");
            model.SubagentNamesUnavailable.ShouldBeTrue();
            view.NamesUnavailableNotice.IsVisible.ShouldBeTrue();

            clock.Advance(TimeSpan.FromSeconds(5));
            model.RefreshElapsed();
            SettingsTree.SettleSafe(window);

            model.Rows[0].Elapsed.ShouldBe(TimeSpan.FromSeconds(5));
            model.Rows[0].ElapsedText.ShouldBe("0:05");
            model.Rows[0].Name.ShouldBe("Sub-agent 1");
            window.Close();
        }, token).ConfigureAwait(true);
    }
}
