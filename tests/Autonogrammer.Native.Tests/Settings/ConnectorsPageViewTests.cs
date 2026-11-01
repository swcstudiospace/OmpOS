#pragma warning disable MA0002
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings.Connectors;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>Headless connectors cards: state chip, check button and per-tool control, and a check contacts one server.</summary>
public sealed class ConnectorsPageViewTests
{
    [Fact]
    public async Task RendersOneCardPerServerWithChipButtonAndToolControl()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var matrix = new ConnectorMatrixModel(new IdleStore());
            var cards = new[]
            {
                new ConnectorCardModel("alpha", "Alpha", ConnectorToolState.NeedsApproval, [new ConnectorToolRow("alpha", "echo", ConnectorToolState.NeedsApproval)], "Connected"),
                new ConnectorCardModel("beta", "Beta", ConnectorToolState.Blocked, [new ConnectorToolRow("beta", "rm", ConnectorToolState.Blocked)], "Failed"),
            };
            var view = new ConnectorsPageView();
            view.Show(matrix, cards, checks: null);
            var window = new Window { Width = 900, Height = 800, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);
            try
            {
                SettingsTree.Texts(view).ShouldContain(ConnectorMatrixModel.ReadOnlyHintNotice);
                SettingsTree.Texts(view).ShouldContain("Connected");
                SettingsTree.Texts(view).ShouldContain("Failed");
                SettingsTree.Has<Button>(view, "Check connection alpha").ShouldBeTrue();
                SettingsTree.Has<Button>(view, "Check connection beta").ShouldBeTrue();
                SettingsTree.Has<ComboBox>(view, "Tool echo on alpha").ShouldBeTrue();
                SettingsTree.Has<ComboBox>(view, "Tool rm on beta").ShouldBeTrue();
                SettingsTree.Has<Border>(view, "Connection state Alpha").ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task CheckConnection_ContactsThatServerOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var source = new RecordingSource();
            var matrix = new ConnectorMatrixModel(new IdleStore());
            var cards = new[]
            {
                new ConnectorCardModel("alpha", "Alpha", ConnectorToolState.NeedsApproval),
                new ConnectorCardModel("beta", "Beta", ConnectorToolState.NeedsApproval),
            };
            var view = new ConnectorsPageView();
            view.Show(matrix, cards, source);
            var window = new Window { Width = 900, Height = 800, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);
            try
            {
                var button = SettingsTree.Named<Button>(view, "Check connection alpha");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                source.Checked.ShouldBe(["alpha"]);
                SettingsTree.Texts(view).ShouldContain("Connected");
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

    private sealed class RecordingSource : IConnectorCheckSource
    {
        public List<string> Checked { get; } = [];

        public Task<ConnectorCheckSnapshot> CheckAsync(string serverId, CancellationToken cancellationToken)
        {
            Checked.Add(serverId);
            return Task.FromResult(new ConnectorCheckSnapshot("Connected", ["echo"]));
        }
    }
}

#pragma warning restore MA0002
