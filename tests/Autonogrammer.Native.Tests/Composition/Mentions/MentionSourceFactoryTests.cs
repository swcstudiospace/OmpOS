using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Composition.Mentions;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition.Mentions;

/// <summary>The list of sources a chat session's mention picker is built from.</summary>
public sealed class MentionSourceFactoryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static MentionSourceContext ContextFor(SessionCatalogModel catalog, string root) => new()
    {
        SessionId = "chat-1",
        WorkspaceRoot = root,
        Catalog = catalog,
        ReadTranscript = (id, _, _) => Task.FromResult(new MentionContent("transcript of " + id, 12, false)),
    };

    /// <summary>The factory returns the ten sources in the order of the wiring, with the kind each one serves, and touches neither disk nor git: a workspace that does not exist does not throw.</summary>
    [Fact]
    public void TheTenSourcesAreBuiltInOrderOverAWorkspaceThatDoesNotExist()
    {
        using var catalog = new SessionCatalogModel(new FakeSessionCatalog(), TimeProvider.System);

        var sources = MentionSourceFactory.Create(ContextFor(catalog, Path.Combine(Path.GetTempPath(), "autonogrammer-mentions-absent-" + Guid.NewGuid().ToString("N"))));

        sources.Select(static source => source.Kind).ShouldBe(
            [
                MentionKind.File,
                MentionKind.Folder,
                MentionKind.Terminal,
                MentionKind.Chat,
                MentionKind.SideChat,
                MentionKind.GitDiffWorking,
                MentionKind.GitDiffBranch,
                MentionKind.Rule,
                MentionKind.Skill,
                MentionKind.PullRequest,
            ]);
    }

    /// <summary>The file source lists the workspace through the ignore files and the global list; the folder source lists folders only.</summary>
    [Fact]
    public async Task TheFileAndFolderSourcesSearchTheWorkspaceAndHonourTheIgnoreSettings()
    {
        using var tree = new MentionWorkspaceTree();
        tree.Write("src/Program.cs", "x");
        tree.Write("src/generated.cs", "x");
        tree.Write("src/notes.log", "x");
        tree.Write(".gitignore", "*.log\n");
        using var catalog = new SessionCatalogModel(new FakeSessionCatalog(), TimeProvider.System);
        var context = ContextFor(catalog, tree.Root) with { GlobalIgnorePatterns = ["generated.cs"] };

        var sources = MentionSourceFactory.Create(context);
        var files = await sources.Single(static source => source.Kind == MentionKind.File).SearchAsync("src/", 20, Token).ConfigureAwait(true);
        var folders = await sources.Single(static source => source.Kind == MentionKind.Folder).SearchAsync("src", 20, Token).ConfigureAwait(true);

        files.Items.Select(static item => item.Id).ShouldBe(["src/Program.cs"], ignoreOrder: true);
        folders.Items.Select(static item => item.Id).ShouldContain("src", StringComparer.Ordinal);
    }

    /// <summary>A source the head supplies replaces the local one: a remote file source is the one the file source reads.</summary>
    [Fact]
    public async Task ASuppliedFileSourceReplacesTheLocalOne()
    {
        using var catalog = new SessionCatalogModel(new FakeSessionCatalog(), TimeProvider.System);
        var context = ContextFor(catalog, "/does/not/matter") with { Files = new FixedMentionFiles(["remote/only.txt"]) };

        var sources = MentionSourceFactory.Create(context);
        var files = await sources.Single(static source => source.Kind == MentionKind.File).SearchAsync("only", 20, Token).ConfigureAwait(true);

        files.Items.Select(static item => item.Id).ShouldBe(["remote/only.txt"], ignoreOrder: true);
    }

    /// <summary>With no terminal reader the terminal mention reads as a plain sentence, not as a crash.</summary>
    [Fact]
    public async Task ATerminalWithoutAReaderIsUnavailableWithASentence()
    {
        var fake = new FakeSessionCatalog();
        var now = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
        fake.Sessions.Add(PanelFixtures.Summary("shell-1", SessionLifecycle.Running, now, now, "zsh") with { Kind = SessionKind.Terminal });
        using var catalog = new SessionCatalogModel(fake, TimeProvider.System);
        catalog.Refresh();
        var sources = MentionSourceFactory.Create(ContextFor(catalog, "/does/not/matter"));

        var terminals = await sources.Single(static source => source.Kind == MentionKind.Terminal).SearchAsync(string.Empty, 5, Token).ConfigureAwait(true);
        var resolved = await sources.Single(static source => source.Kind == MentionKind.Terminal).ResolveAsync(terminals.Items.Single(), Token).ConfigureAwait(true);
        var refused = await Should.ThrowAsync<MentionUnavailableException>(() => resolved.ReadAsync(1000, Token)).ConfigureAwait(true);

        refused.Message.ShouldBe("The output of this terminal is not available in this window.");
    }

    /// <summary>With no pull request source the pull request mention is the unavailable one.</summary>
    [Fact]
    public async Task PullRequestsAreUnavailableWithoutASource()
    {
        using var catalog = new SessionCatalogModel(new FakeSessionCatalog(), TimeProvider.System);
        var sources = MentionSourceFactory.Create(ContextFor(catalog, "/does/not/matter"));

        var result = await sources.Single(static source => source.Kind == MentionKind.PullRequest).SearchAsync(string.Empty, 5, Token).ConfigureAwait(true);

        result.Items.ShouldBeEmpty();
        result.UnavailableReason.ShouldBe(MentionCopy.PullRequestsNoHost);
    }
}
