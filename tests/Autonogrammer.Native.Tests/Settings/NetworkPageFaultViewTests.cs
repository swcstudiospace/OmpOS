using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>A run that faults outside the source's refusal is announced as a failed run, and the page is ready to run again.</summary>
public sealed class NetworkPageFaultViewTests
{
    [Fact]
    public async Task AnUnexpectedFaultIsAnnouncedAsARunThatCouldNotBeRun()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(async () =>
        {
            var page = new NetworkSettingsPage(new FaultingSource());
            await page.LoadAsync(ct).ConfigureAwait(true);
            var view = new NetworkPageView { DataContext = page };
            var window = new Window { Width = 900, Height = 800, Content = view };
            window.Show();
            try
            {
                SettingsTree.Click(window, view.Run);
                SettingsTree.SettleSafe(window);

                view.Announcement.Text.ShouldBe(NetworkCopy.RunFailed);
                view.Announcement.IsVisible.ShouldBeTrue();
                page.IsRunning.ShouldBeFalse();
                page.CanRun.ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    private sealed class FaultingSource : INetworkDiagnosticsSource
    {
        public Task<NetworkDiagnosticsReport> RunAsync(CancellationToken cancellationToken) =>
            Task.FromException<NetworkDiagnosticsReport>(new InvalidOperationException("The source is defective."));
    }
}
