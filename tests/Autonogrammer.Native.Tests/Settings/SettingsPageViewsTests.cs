using Autonogrammer.Native.Views.Dispatch;
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Dispatch;
using Autonogrammer.Presentation.Settings;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The Dispatch view draws its relay's current page: a load that completes while the view is away is shown once it returns.</summary>
public sealed class SettingsPageViewsTests
{
    /// <summary>A relay load that completes while the Dispatch view is detached is drawn when the view is attached again.</summary>
    [Fact]
    public async Task DispatchViewShowsThePageLoadedWhileItWasDetached()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = new FakeDispatchHost([new DispatchDevice("device-1", "Office Mac", Online: true, PresenceKnown: true)]);
            var relay = new DispatchRelayModel(host, host);
            var view = SettingsPageViews.Create(new DispatchSettingsPage(relay)).ShouldBeOfType<DispatchPageView>();
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            window.Content = null;
            ShellHarness.Settle(window);
            var loading = relay.Page;
            await relay.LoadAsync(ct).ConfigureAwait(true);
            relay.Page.ShouldNotBeSameAs(loading);

            window.Content = view;
            ShellHarness.Settle(window);
            try
            {
                view.DataContext.ShouldBeSameAs(relay.Page);
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }
}
