using OmpOS.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests.Conventions;

/// <summary>Discovery reads only through <see cref="IConventionFileSource"/>: the same code serves an in-memory tree that stands for the SSH and cloud file trees of later phases.</summary>
public sealed class ConventionCatalogSourceTests
{
    private static ConventionSources Sources(IConventionFileSource project, IConventionFileSource user) => new(project, user);

    /// <summary>The remote workspace and the local home are two different sources: project items come from the first, user items from the second, and no disk path is involved.</summary>
    [Fact]
    public async Task ProjectAndUserItemsComeFromTheirOwnSources()
    {
        var remote = new InMemoryConventionFileSource();
        remote.Add("/srv/ws/.cursor/commands/deploy.md", "---\ndescription: Deploy\n---\nDeploy $ARGUMENTS\n");
        remote.Add("/srv/ws/.claude/skills/audit/SKILL.md", "---\ndescription: Audit\n---\n");
        remote.Add("/srv/ws/AGENTS.md", "Remote rules.\n");
        remote.Add("/srv/ws/.cursor/hooks.json", "{}");
        remote.Add("/srv/ws/pkg/.cursor/skills/nested/SKILL.md", "Nested.\n");
        var local = new InMemoryConventionFileSource();
        local.Add("/home/dev/.cursor/commands/standup.md", "Standup\n");
        local.Add("/home/dev/.agents/skills/audit/SKILL.md", "User audit.\n");

        var set = await ConventionCatalog.DiscoverAsync(["/srv/ws"], "/home/dev", Sources(remote, local), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        set.Commands.Select(command => (command.Name, command.Scope, command.Path)).ShouldBe(
        [
            ("deploy", ConventionScope.Project, "/srv/ws/.cursor/commands/deploy.md"),
            ("standup", ConventionScope.User, "/home/dev/.cursor/commands/standup.md"),
        ]);
        set.Skills.Select(skill => (skill.Name, skill.Scope, skill.ScopeDirectory)).ShouldBe(
        [
            ("audit", ConventionScope.Project, string.Empty),
            ("nested", ConventionScope.Project, "pkg"),
        ]);
        set.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(ConventionDiagnosticKind.Duplicate);
        set.Rules.ShouldHaveSingleItem().Path.ShouldBe("/srv/ws/AGENTS.md");
        set.HookFiles.ShouldHaveSingleItem().Path.ShouldBe("/srv/ws/.cursor/hooks.json");
        remote.Read.ShouldAllBe(path => path.StartsWith("/srv/ws/", StringComparison.Ordinal));
        local.Read.ShouldAllBe(path => path.StartsWith("/home/dev/", StringComparison.Ordinal));
    }

    /// <summary>Hooks and MCP files are listed without being read.</summary>
    [Fact]
    public async Task ConfigFilesAreNeverRead()
    {
        var remote = new InMemoryConventionFileSource();
        remote.Add("/ws/.cursor/hooks.json", "{}");
        remote.Add("/ws/.cursor/mcp.json", "{}");
        remote.Add("/ws/.mcp.json", "{}");

        var set = await ConventionCatalog.DiscoverAsync(["/ws"], null, Sources(remote, remote), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        set.HookFiles.Count.ShouldBe(1);
        set.McpFiles.Count.ShouldBe(2);
        remote.Read.ShouldBeEmpty();
    }

    /// <summary>A source that cannot list a folder yields a diagnostic with the source's own kind and reason, and the other folders still load.</summary>
    [Fact]
    public async Task AFolderThatCannotBeListedIsADiagnostic()
    {
        var remote = new InMemoryConventionFileSource();
        remote.Add("/ws/.cursor/commands/ok.md", "Fine\n");
        remote.Add("/ws/.claude/commands/lost.md", "Lost\n");
        remote.Break("/ws/.claude/commands", ConventionDiagnosticKind.Unreadable, "connection reset");

        var set = await ConventionCatalog.DiscoverAsync(["/ws"], null, Sources(remote, remote), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        set.Commands.Select(command => command.Name).ShouldBe(["ok"]);
        var diagnostic = set.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Item.ShouldBe(ConventionItemKind.Command);
        diagnostic.Kind.ShouldBe(ConventionDiagnosticKind.Unreadable);
        diagnostic.Origin.ShouldBe(".claude/commands");
        diagnostic.Reason.ShouldBe("connection reset");
    }

    /// <summary>The size cap is passed to the source, and a source's TooLarge answer becomes a diagnostic.</summary>
    [Fact]
    public async Task TheSizeCapIsEnforcedThroughTheSource()
    {
        var remote = new InMemoryConventionFileSource();
        remote.Add("/ws/.cursor/commands/big.md", new string('x', 500));
        remote.Add("/ws/.cursor/commands/small.md", "ok");

        var set = await ConventionCatalog.DiscoverAsync(
            ["/ws"],
            null,
            Sources(remote, remote),
            new ConventionDiscoveryOptions { MaxFileBytes = 50 },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        set.Commands.Select(command => command.Name).ShouldBe(["small"]);
        set.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(ConventionDiagnosticKind.TooLarge);
    }

    /// <summary>The nested walk is bounded by the folder count: a tree with many folders is not listed past the bound.</summary>
    [Fact]
    public async Task TheNestedWalkIsBoundedByTheFolderCount()
    {
        var remote = new InMemoryConventionFileSource();
        for (var index = 0; index < 50; index++)
        {
            remote.Add("/ws/dir" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "/file.txt", "x");
        }

        await ConventionCatalog.DiscoverAsync(
            ["/ws"],
            null,
            Sources(remote, remote),
            new ConventionDiscoveryOptions { MaxNestedDirectories = 5 },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        remote.Listed.Count(path => path.StartsWith("/ws/dir", StringComparison.Ordinal)).ShouldBe(5);
    }

    /// <summary>The cancellation token reaches the source.</summary>
    [Fact]
    public async Task TheCancellationTokenReachesTheSource()
    {
        var remote = new InMemoryConventionFileSource();
        remote.Add("/ws/.cursor/commands/x.md", "x");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(
            () => ConventionCatalog.DiscoverAsync(["/ws"], null, Sources(remote, remote), cancellationToken: cancelled.Token)).ConfigureAwait(true);
    }
}
