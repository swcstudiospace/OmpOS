using Autonogrammer.Presentation.Dispatch;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Dispatch;

/// <summary>The Dispatch settings section: its section and title, the devices it reads through the host port, and the change it raises once ready.</summary>
public sealed class DispatchSettingsPageTests
{
    /// <summary>The page is the Dispatch section and is titled from the Dispatch copy.</summary>
    [Fact]
    public void The_page_is_the_dispatch_section_titled_from_the_copy()
    {
        var page = new DispatchSettingsPage(new DispatchRelayModel(new NotConfiguredDispatchHost(), null));

        page.Section.ShouldBe(SettingsSection.Dispatch);
        page.Title.ShouldBe(DispatchCopy.PageHeading);
    }

    /// <summary>Loading the page reads the account's devices through the host port.</summary>
    [Fact]
    public async Task Loading_the_page_reads_the_devices_through_the_host()
    {
        var host = new FakeDispatchHost([new DispatchDevice("device-1", "Office Mac", Online: true, PresenceKnown: true)]);
        var relay = new DispatchRelayModel(host, host);
        var page = new DispatchSettingsPage(relay);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        relay.Page.Devices.Rows.Count.ShouldBe(1);
        relay.Page.Devices.Rows[0].Name.ShouldBe("Office Mac");
    }

    /// <summary>The model raises its page change once the page is ready, so the view can draw it.</summary>
    [Fact]
    public async Task The_model_raises_the_page_change_once_it_is_ready()
    {
        var host = new FakeDispatchHost([]);
        var relay = new DispatchRelayModel(host, null);
        var raised = new List<string?>();
        relay.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        await new DispatchSettingsPage(relay).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        raised.ShouldBe(["Page"]);
    }
}
