using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings.Connectors;
using Autonogrammer.Presentation.Settings.Cowork;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The Connectors view the settings window draws shows the servers its page loaded.</summary>
public sealed class ConnectorsSettingsViewTests
{
    [Fact]
    public async Task TheSettingsViewDrawsTheServersThePageLoaded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(async () =>
        {
            var page = new ConnectorsSettingsPage(
                new IdleStore(),
                _ => Task.FromResult<IReadOnlyList<ConnectorServerListing>>([new ConnectorServerListing("alpha", "Alpha")]),
                new NoChecks());
            var view = SettingsPageViews.Create(page);
            await page.LoadAsync(ct).ConfigureAwait(true);
            var window = new Window { Width = 900, Height = 800, Content = view };
            window.Show();
            try
            {
                SettingsTree.SettleSafe(window);

                SettingsTree.Has<Border>(view, "Connection state Alpha").ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    private sealed class IdleStore : IConnectorPermissionStore
    {
        public IReadOnlyList<string> ReadAllowTokens() => [];

        public IReadOnlyList<string> ReadDenyTokens() => [];

        public void ReplaceAllowTokens(IReadOnlyList<string> tokens)
        {
        }

        public void ReplaceDenyTokens(IReadOnlyList<string> tokens)
        {
        }
    }

    private sealed class NoChecks : IConnectorCheckSource
    {
        public Task<ConnectorCheckSnapshot> CheckAsync(string serverId, CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectorCheckSnapshot("Connected", []));
    }
}
