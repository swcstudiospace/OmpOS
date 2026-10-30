using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Mentions;
using Autonogrammer.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Mentions;

/// <summary>The session catalog as a source of chats and terminals for mentions.</summary>
public sealed class CatalogMentionProviderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (CatalogMentionProvider Provider, SessionCatalogModel Rail) Create(string? current, IReadOnlyList<MentionChatInfo>? sideChats = null)
    {
        var clock = new ManualTimeProvider();
        var catalog = new FakeSessionCatalog();
        var now = clock.GetUtcNow();
        catalog.Sessions.Add(AgentPanelTestData.Summary("chat-new", SessionLifecycle.Running, now, now, "Newer chat"));
        catalog.Sessions.Add(AgentPanelTestData.Summary("chat-old", SessionLifecycle.Completed, now.AddHours(-3), now.AddHours(-2), "Older chat"));
        catalog.Sessions.Add(AgentPanelTestData.Summary("shell-1", SessionLifecycle.Running, now, now, "zsh") with { Kind = SessionKind.Terminal });
        var rail = new SessionCatalogModel(catalog, clock);
        var provider = new CatalogMentionProvider(
            rail,
            (id, _, _) => Task.FromResult(new MentionContent("transcript of " + id, 20, false)),
            (id, _, _) => Task.FromResult(new MentionContent("output of " + id, 15, false)),
            sideChats is null ? null : () => sideChats,
            () => current);
        return (provider, rail);
    }

    /// <summary>Chats are the catalog's chat rows without the open session; terminals are its terminal rows; the two never mix.</summary>
    [Fact]
    public async Task ChatsAndTerminalsAreSeparatedAndTheOpenSessionIsLeftOut()
    {
        var (provider, rail) = Create(current: "chat-new");
        using var _ = rail;

        var chats = await ((IMentionChatProvider)provider).ListAsync(Token).ConfigureAwait(true);
        var terminals = await ((IMentionTerminalProvider)provider).ListAsync(Token).ConfigureAwait(true);

        chats.Select(static chat => chat.Id).ShouldBe(["chat-old"]);
        chats.Single().IsSideChat.ShouldBeFalse();
        terminals.Select(static terminal => terminal.Id).ShouldBe(["shell-1"]);
        terminals.Single().IsRunning.ShouldBeTrue();
    }

    /// <summary>Side chats the head supplies are listed after the catalog's chats, flagged as side chats.</summary>
    [Fact]
    public async Task SideChatsComeFromTheHead()
    {
        var side = new MentionChatInfo("side-1", "Side question", true, new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero));
        var (provider, rail) = Create(current: null, [side]);
        using var _ = rail;

        var chats = await ((IMentionChatProvider)provider).ListAsync(Token).ConfigureAwait(true);

        chats.Select(static chat => chat.Id).ShouldBe(["chat-new", "chat-old", "side-1"]);
        chats[^1].IsSideChat.ShouldBeTrue();
    }

    /// <summary>The reads go through the delegates the head supplied.</summary>
    [Fact]
    public async Task ReadsGoThroughTheHeadsDelegates()
    {
        var (provider, rail) = Create(current: null);
        using var _ = rail;

        (await provider.ReadTranscriptAsync("chat-old", 100, Token).ConfigureAwait(true)).Text.ShouldBe("transcript of chat-old");
        (await provider.ReadOutputAsync("shell-1", 100, Token).ConfigureAwait(true)).Text.ShouldBe("output of shell-1");
    }
}
