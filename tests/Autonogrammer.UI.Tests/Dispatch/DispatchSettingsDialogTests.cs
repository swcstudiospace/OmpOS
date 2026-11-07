using Autonogrammer.Presentation.Dispatch;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.UI.Components.Settings;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Dispatch;

/// <summary>The web settings dialog draws the Dispatch section: its page carries the Dispatch title, and it draws with no host supplied.</summary>
public sealed class DispatchSettingsDialogTests : UiTestContext
{
    /// <summary>The Dispatch page is drawn with its title and without a host, so the section is reachable on the web head before pairing is wired.</summary>
    [Fact]
    public void The_dispatch_section_draws_its_page_without_a_host()
    {
        var page = new DispatchSettingsPage(new DispatchRelayModel(new NotConfiguredDispatchHost(), null));
        var window = new SettingsWindowModel([page]);

        var cut = Render<SettingsDialog>(parameters => parameters.Add(p => p.Settings, window).Add(p => p.IsOpen, true));

        cut.Find("#dispatch-page-heading").TextContent.ShouldBe(DispatchCopy.PageHeading);
        cut.FindAll(".autonogrammer-settings-state").Count.ShouldBe(0);
    }
}
