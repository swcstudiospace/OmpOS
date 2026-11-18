using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Connectors;
using Autonogrammer.Presentation.Settings.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The connectors page lists its servers when it loads, shows each one's stored policy, and keeps its cards live across loads.</summary>
public sealed class ConnectorsSettingsPageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LoadingListsOneCardPerServerWithItsStoredPolicy()
    {
        var store = new MemoryStore();
        store.ReplaceAllowTokens(["Mcp(alpha:*)"]);
        var page = new ConnectorsSettingsPage(store, _ => Task.FromResult(Servers(("alpha", "Alpha"), ("beta", "Beta"))), new NoChecks());

        page.State.ShouldBe(SettingsPageState.Loading);
        await page.LoadAsync(Token);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.Cards.Select(card => (card.ServerId, card.DisplayName, card.ServerPermission)).ToArray()
            .ShouldBe([("alpha", "Alpha", ConnectorToolState.AlwaysAllow), ("beta", "Beta", ConnectorToolState.NeedsApproval)]);
    }

    [Fact]
    public async Task AFailedListingShowsTheReasonAndNoCards()
    {
        var page = new ConnectorsSettingsPage(
            new MemoryStore(),
            _ => throw new SettingsSourceException("The configuration could not be read."),
            new NoChecks());

        await page.LoadAsync(Token);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("The configuration could not be read.");
        page.Cards.ShouldBeEmpty();
    }

    [Fact]
    public async Task EachLoadReplacesTheCardsInTheSameCollection()
    {
        IReadOnlyList<ConnectorServerListing> servers = Servers(("alpha", "Alpha"), ("beta", "Beta"));
        var page = new ConnectorsSettingsPage(new MemoryStore(), _ => Task.FromResult(servers), new NoChecks());
        var cards = page.Cards;

        await page.LoadAsync(Token);
        cards.Count.ShouldBe(2);

        servers = Servers(("gamma", "Gamma"));
        await page.LoadAsync(Token);

        cards.ShouldBeSameAs(page.Cards);
        cards.Select(card => card.ServerId).ToArray().ShouldBe(["gamma"]);
    }

    private static IReadOnlyList<ConnectorServerListing> Servers(params (string Id, string Name)[] items) =>
        [.. items.Select(item => new ConnectorServerListing(item.Id, item.Name))];

    private sealed class MemoryStore : IConnectorPermissionStore
    {
        private List<string> _allow = [];
        private List<string> _deny = [];

        public IReadOnlyList<string> ReadAllowTokens() => _allow;

        public IReadOnlyList<string> ReadDenyTokens() => _deny;

        public void ReplaceAllowTokens(IReadOnlyList<string> tokens) => _allow = [.. tokens];

        public void ReplaceDenyTokens(IReadOnlyList<string> tokens) => _deny = [.. tokens];
    }

    private sealed class NoChecks : IConnectorCheckSource
    {
        public Task<ConnectorCheckSnapshot> CheckAsync(string serverId, CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectorCheckSnapshot("Not checked", []));
    }
}
