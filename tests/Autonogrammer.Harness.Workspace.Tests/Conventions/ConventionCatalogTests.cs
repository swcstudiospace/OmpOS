using Autonogrammer.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests.Conventions;

/// <summary>Discovery over real temporary trees: every convention directory at both scopes, precedence, duplicates, and the files that must never break a run (PX-13).</summary>
public sealed class ConventionCatalogTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();
    private readonly TempWorkspace _home = new();

    /// <inheritdoc/>
    public void Dispose()
    {
        _workspace.Dispose();
        _home.Dispose();
    }

    public static TheoryData<string, ConventionScope> DirectoriesAndScopes()
    {
        var data = new TheoryData<string, ConventionScope>();
        foreach (var directory in ConventionDirectories.Names)
        {
            data.Add(directory, ConventionScope.Project);
            data.Add(directory, ConventionScope.User);
        }

        return data;
    }

    private Task<ConventionSet> DiscoverAsync(ConventionDiscoveryOptions? options = null) =>
        ConventionCatalog.DiscoverAsync([_workspace.RootPath], _home.RootPath, ConventionSources.Local([_workspace.RootPath], _home.RootPath), options, TestContext.Current.CancellationToken);

    private void WriteIn(ConventionScope scope, string relativePath, string content) =>
        (scope == ConventionScope.Project ? _workspace : _home).Write(relativePath, content);

    /// <summary>The six convention directories are scanned at the workspace root and at the user's home: a command, a skill, a rule and a subagent in each is found with its directory and scope.</summary>
    [Theory]
    [MemberData(nameof(DirectoriesAndScopes))]
    public async Task EveryConventionDirectoryIsScannedAtBothScopes(string directory, ConventionScope scope)
    {
        WriteIn(scope, directory + "/commands/hello.md", "---\ndescription: Says hello\nargument-hint: <name>\n---\nHello $1\n");
        WriteIn(scope, directory + "/skills/alpha/SKILL.md", "---\nname: alpha\ndescription: Alpha skill\n---\nDo alpha.\n");
        WriteIn(scope, directory + "/rules/style.mdc", "---\ndescription: Style\nglobs: **/*.cs\nalwaysApply: false\n---\nUse spaces.\n");
        WriteIn(scope, directory + "/agents/reviewer.md", "---\nname: reviewer\ndescription: Reviews\nmodel: inherit\nreadonly: true\nis_background: true\n---\nReview it.\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Diagnostics.ShouldBeEmpty();
        var origin = (scope == ConventionScope.User ? "~/" : string.Empty) + directory;
        var command = set.Commands.ShouldHaveSingleItem();
        command.Name.ShouldBe("hello");
        command.Description.ShouldBe("Says hello");
        command.ArgumentHint.ShouldBe("<name>");
        command.Template.ShouldBe("Hello $1\n");
        command.Scope.ShouldBe(scope);
        command.SourceDirectory.ShouldBe(directory);
        command.Origin.ShouldBe(origin + "/commands/hello.md");
        var skill = set.Skills.ShouldHaveSingleItem();
        skill.Name.ShouldBe("alpha");
        skill.Scope.ShouldBe(scope);
        skill.SourceDirectory.ShouldBe(directory);
        skill.Origin.ShouldBe(origin + "/skills/alpha/SKILL.md");
        var rule = set.Rules.ShouldHaveSingleItem();
        rule.Name.ShouldBe("style");
        rule.Kind.ShouldBe(ConventionRuleKind.Mdc);
        rule.Globs.ShouldBe(["**/*.cs"]);
        rule.AlwaysApply.ShouldBeFalse();
        rule.Scope.ShouldBe(scope);
        var agent = set.Subagents.ShouldHaveSingleItem();
        agent.Name.ShouldBe("reviewer");
        agent.Model.ShouldBe("inherit");
        agent.ReadOnly.ShouldBeTrue();
        agent.IsBackground.ShouldBeTrue();
        agent.Instructions.ShouldBe("Review it.\n");
        agent.Scope.ShouldBe(scope);
    }

    /// <summary>Within a scope the directories rank .autonogrammer, .omp, .cursor, .claude, .codex, .agents: the first with a name wins, and every loser is a Duplicate diagnostic that names the winner.</summary>
    [Fact]
    public async Task WithinAScopeTheDirectoryOrderDecidesAndLosersAreDiagnostics()
    {
        foreach (var directory in ConventionDirectories.Names.Reverse())
        {
            _workspace.Write(directory + "/commands/deploy.md", "Deploy from " + directory + "\n");
        }

        var set = await DiscoverAsync().ConfigureAwait(true);

        var winner = set.Commands.ShouldHaveSingleItem();
        winner.SourceDirectory.ShouldBe(".autonogrammer");
        winner.Template.ShouldBe("Deploy from .autonogrammer\n");
        set.Diagnostics.Count.ShouldBe(5);
        set.Diagnostics.ShouldAllBe(diagnostic => diagnostic.Kind == ConventionDiagnosticKind.Duplicate && diagnostic.Item == ConventionItemKind.Command);
        set.Diagnostics.Select(diagnostic => diagnostic.Origin).ShouldBe([".omp/commands/deploy.md", ".cursor/commands/deploy.md", ".claude/commands/deploy.md", ".codex/commands/deploy.md", ".agents/commands/deploy.md"]);
        set.Diagnostics.ShouldAllBe(diagnostic => diagnostic.Reason.EndsWith(".autonogrammer/commands/deploy.md", StringComparison.Ordinal));
    }

    /// <summary>A project item beats a user item of the same name, for every kind; the user item is a Duplicate diagnostic.</summary>
    [Fact]
    public async Task AProjectItemBeatsAUserItem()
    {
        _home.Write(".autonogrammer/commands/ship.md", "User ship\n");
        _workspace.Write(".agents/commands/ship.md", "Project ship\n");
        _home.Write(".autonogrammer/skills/lint/SKILL.md", "User lint\n");
        _workspace.Write(".agents/skills/lint/SKILL.md", "Project lint\n");
        _home.Write(".autonogrammer/agents/checker.md", "User checker\n");
        _workspace.Write(".agents/agents/checker.md", "Project checker\n");
        _home.Write(".autonogrammer/rules/style.md", "User style\n");
        _workspace.Write(".agents/rules/style.md", "Project style\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Commands.ShouldHaveSingleItem().Scope.ShouldBe(ConventionScope.Project);
        set.Skills.ShouldHaveSingleItem().Scope.ShouldBe(ConventionScope.Project);
        set.Subagents.ShouldHaveSingleItem().Scope.ShouldBe(ConventionScope.Project);
        set.Rules.ShouldHaveSingleItem().Scope.ShouldBe(ConventionScope.Project);
        set.Diagnostics.Count.ShouldBe(4);
        set.Diagnostics.ShouldAllBe(diagnostic => diagnostic.Kind == ConventionDiagnosticKind.Duplicate && diagnostic.Origin.StartsWith("~/.autonogrammer/", StringComparison.Ordinal));
    }

    /// <summary>Items of different names from both scopes are all kept, project first and then by name.</summary>
    [Fact]
    public async Task ItemsAreListedProjectFirstThenByName()
    {
        _home.Write(".cursor/commands/alpha.md", "A\n");
        _workspace.Write(".cursor/commands/zeta.md", "Z\n");
        _workspace.Write(".cursor/commands/beta.md", "B\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Commands.Select(command => command.Name).ShouldBe(["beta", "zeta", "alpha"]);
        set.Commands.Select(command => command.Scope).ShouldBe([ConventionScope.Project, ConventionScope.Project, ConventionScope.User]);
    }

    /// <summary>When several workspace roots are open, the earlier root wins a shared name.</summary>
    [Fact]
    public async Task AnEarlierWorkspaceRootWinsASharedName()
    {
        using var second = new TempWorkspace();
        _workspace.Write(".cursor/commands/shared.md", "First root\n");
        second.Write(".cursor/commands/shared.md", "Second root\n");
        second.Write(".cursor/commands/only-second.md", "Only second\n");

        var set = await ConventionCatalog.DiscoverAsync(
            [_workspace.RootPath, second.RootPath],
            null,
            ConventionSources.Local([_workspace.RootPath, second.RootPath], null),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        set.Commands.Select(command => command.Name).ShouldBe(["only-second", "shared"]);
        set.Commands.Single(command => string.Equals(command.Name, "shared", StringComparison.Ordinal)).Template.ShouldBe("First root\n");
        set.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(ConventionDiagnosticKind.Duplicate);
    }

    /// <summary>Frontmatter that cannot be read is a Malformed diagnostic and the file yields no item; the other files still load.</summary>
    [Fact]
    public async Task MalformedFrontmatterIsADiagnosticAndNeverAnException()
    {
        _workspace.Write(".cursor/commands/good.md", "---\ndescription: Good\n---\nBody\n");
        _workspace.Write(".cursor/commands/bad.md", "---\ndescription Good\n---\nBody\n");
        _workspace.Write(".cursor/skills/broken/SKILL.md", "---\nname: broken\n");
        _workspace.Write(".cursor/rules/loose.mdc", "---\nglobs: [a\n---\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Commands.Select(command => command.Name).ShouldBe(["good"]);
        set.Skills.ShouldBeEmpty();
        set.Rules.ShouldBeEmpty();
        set.Diagnostics.ShouldAllBe(diagnostic => diagnostic.Kind == ConventionDiagnosticKind.Malformed);
        set.Diagnostics.Select(diagnostic => diagnostic.Origin).Order(StringComparer.Ordinal)
            .ShouldBe([".cursor/commands/bad.md", ".cursor/rules/loose.mdc", ".cursor/skills/broken/SKILL.md"]);
        set.Diagnostics.Single(diagnostic => diagnostic.Item == ConventionItemKind.Command).Reason.ShouldStartWith("malformed frontmatter: ", Case.Sensitive);
    }

    /// <summary>A file larger than the cap is a TooLarge diagnostic, and a file that is not UTF-8 is Malformed; neither is read into an item.</summary>
    [Fact]
    public async Task OversizeAndNonUtf8FilesAreDiagnostics()
    {
        _workspace.Write(".cursor/commands/small.md", "ok\n");
        _workspace.Write(".cursor/commands/huge.md", new string('x', 300));
        _workspace.WriteBytes(".cursor/commands/binary.md", [0xFF, 0xFE, 0x41, 0x80]);

        var set = await DiscoverAsync(new ConventionDiscoveryOptions { MaxFileBytes = 100 }).ConfigureAwait(true);

        set.Commands.Select(command => command.Name).ShouldBe(["small"]);
        set.Diagnostics.Single(diagnostic => diagnostic.Origin.EndsWith("huge.md", StringComparison.Ordinal)).Kind.ShouldBe(ConventionDiagnosticKind.TooLarge);
        set.Diagnostics.Single(diagnostic => diagnostic.Origin.EndsWith("binary.md", StringComparison.Ordinal)).Kind.ShouldBe(ConventionDiagnosticKind.Malformed);
    }

    /// <summary>The default cap is 256 KiB: a file just under it loads, a file just over it is refused.</summary>
    [Fact]
    public async Task TheDefaultSizeCapIs256Kib()
    {
        _workspace.Write(".cursor/commands/under.md", new string('x', (256 * 1024) - 1));
        _workspace.Write(".cursor/commands/over.md", new string('x', (256 * 1024) + 1));

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Commands.Select(command => command.Name).ShouldBe(["under"]);
        set.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(ConventionDiagnosticKind.TooLarge);
    }

    /// <summary>A symbolic link that leaves the workspace root is refused: a linked folder, a linked file, and the link is a diagnostic, not a read.</summary>
    [Fact]
    public async Task ASymbolicLinkThatEscapesTheRootIsRefused()
    {
        using var outside = new TempWorkspace();
        outside.Write("skills/stolen/SKILL.md", "---\nname: stolen\n---\nSecret.\n");
        outside.Write("secret.md", "Secret command.\n");
        Directory.CreateDirectory(Path.Combine(_workspace.RootPath, ".claude"));
        Directory.CreateSymbolicLink(Path.Combine(_workspace.RootPath, ".claude", "skills"), Path.Combine(outside.RootPath, "skills"));
        Directory.CreateDirectory(Path.Combine(_workspace.RootPath, ".cursor", "commands"));
        File.CreateSymbolicLink(Path.Combine(_workspace.RootPath, ".cursor", "commands", "leak.md"), Path.Combine(outside.RootPath, "secret.md"));
        _workspace.Write(".cursor/commands/fine.md", "Fine.\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Skills.ShouldBeEmpty();
        set.Commands.Select(command => command.Name).ShouldBe(["fine"]);
        set.Diagnostics.Count(diagnostic => diagnostic.Kind == ConventionDiagnosticKind.EscapesRoot).ShouldBe(2);
        set.Diagnostics.Any(diagnostic => diagnostic.Item == ConventionItemKind.Skill && string.Equals(diagnostic.Origin, ".claude/skills", StringComparison.Ordinal)).ShouldBeTrue();
        set.Diagnostics.Any(diagnostic => diagnostic.Item == ConventionItemKind.Command && string.Equals(diagnostic.Origin, ".cursor/commands/leak.md", StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>A symbolic link that stays inside the root is followed.</summary>
    [Fact]
    public async Task ASymbolicLinkInsideTheRootIsFollowed()
    {
        _workspace.Write("shared/skills/linked/SKILL.md", "---\nname: linked\ndescription: Linked skill\n---\n");
        Directory.CreateDirectory(Path.Combine(_workspace.RootPath, ".cursor"));
        Directory.CreateSymbolicLink(Path.Combine(_workspace.RootPath, ".cursor", "skills"), Path.Combine(_workspace.RootPath, "shared", "skills"));

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Skills.ShouldHaveSingleItem().Name.ShouldBe("linked");
        set.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>Skills below the root are found in nested scopes, down to the depth bound, and folders such as node_modules and hidden folders are not entered.</summary>
    [Fact]
    public async Task NestedSkillScopesAreFoundWithinTheDepthBound()
    {
        _workspace.Write(".cursor/skills/root-skill/SKILL.md", "Root.\n");
        _workspace.Write("packages/app/.cursor/skills/app-skill/SKILL.md", "---\ndescription: App\n---\n");
        _workspace.Write("packages/app/web/.claude/skills/web-skill/SKILL.md", "Web.\n");
        _workspace.Write("a/b/c/d/e/.cursor/skills/too-deep/SKILL.md", "Deep.\n");
        _workspace.Write("node_modules/pkg/.cursor/skills/vendored/SKILL.md", "Vendored.\n");
        _workspace.Write(".hidden/.cursor/skills/hidden-skill/SKILL.md", "Hidden.\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Skills.Select(skill => skill.Name).ShouldBe(["app-skill", "root-skill", "web-skill"]);
        set.Skills.Single(skill => string.Equals(skill.Name, "app-skill", StringComparison.Ordinal)).ScopeDirectory.ShouldBe("packages/app");
        set.Skills.Single(skill => string.Equals(skill.Name, "web-skill", StringComparison.Ordinal)).Origin.ShouldBe("packages/app/web/.claude/skills/web-skill/SKILL.md");
        set.Skills.Single(skill => string.Equals(skill.Name, "root-skill", StringComparison.Ordinal)).ScopeDirectory.ShouldBe(string.Empty);
    }

    /// <summary>The same skill name in two nested scopes is two skills, not a duplicate; a depth of zero scans the root only.</summary>
    [Fact]
    public async Task NestedScopesDoNotCollideAndDepthZeroScansTheRootOnly()
    {
        _workspace.Write(".cursor/skills/lint/SKILL.md", "Root lint.\n");
        _workspace.Write("one/.cursor/skills/lint/SKILL.md", "One lint.\n");
        _workspace.Write("two/.cursor/skills/lint/SKILL.md", "Two lint.\n");

        var full = await DiscoverAsync().ConfigureAwait(true);
        var rootOnly = await DiscoverAsync(new ConventionDiscoveryOptions { MaxNestedDepth = 0 }).ConfigureAwait(true);

        full.Skills.Select(skill => skill.ScopeDirectory).ShouldBe(["", "one", "two"]);
        full.Diagnostics.ShouldBeEmpty();
        rootOnly.Skills.ShouldHaveSingleItem().ScopeDirectory.ShouldBe(string.Empty);
    }

    /// <summary>Instruction files are always-applied rules: AGENTS.md and CLAUDE.md at the root and nested, .cursorrules at the root, and the ones inside convention directories.</summary>
    [Fact]
    public async Task InstructionFilesAreRules()
    {
        _workspace.Write("AGENTS.md", "# Agents\nUse tabs never.\n");
        _workspace.Write("CLAUDE.md", "Be brief.\n");
        _workspace.Write(".cursorrules", "Legacy rule.\n");
        _workspace.Write("services/api/AGENTS.md", "API notes.\n");
        _workspace.Write(".claude/CLAUDE.md", "Claude dir notes.\n");
        _home.Write(".codex/AGENTS.md", "Home notes.\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Rules.Select(rule => rule.Origin).ShouldBe([".claude/CLAUDE.md", ".cursorrules", "AGENTS.md", "services/api/AGENTS.md", "CLAUDE.md", "~/.codex/AGENTS.md"]);
        var agents = set.Rules.Single(rule => string.Equals(rule.Origin, "AGENTS.md", StringComparison.Ordinal));
        agents.Kind.ShouldBe(ConventionRuleKind.AgentsFile);
        agents.AlwaysApply.ShouldBeTrue();
        agents.Description.ShouldBe("Use tabs never.");
        agents.ScopeDirectory.ShouldBeEmpty();
        set.Rules.Single(rule => string.Equals(rule.Origin, ".cursorrules", StringComparison.Ordinal)).Kind.ShouldBe(ConventionRuleKind.CursorRules);
        set.Rules.Single(rule => string.Equals(rule.Origin, "services/api/AGENTS.md", StringComparison.Ordinal)).ScopeDirectory.ShouldBe("services/api");
        set.Rules.Single(rule => string.Equals(rule.Origin, "~/.codex/AGENTS.md", StringComparison.Ordinal)).Scope.ShouldBe(ConventionScope.User);
        set.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>Rules in subfolders take their relative path as the name, both .mdc and .md are rules, and a rule's flags and globs are read.</summary>
    [Fact]
    public async Task RuleFilesKeepTheirPathAndFlags()
    {
        _workspace.Write(".cursor/rules/frontend/style.mdc", "---\ndescription: Frontend style\nglobs:\n  - web/**/*.ts\n  - web/**/*.tsx\nalwaysApply: true\n---\nBody\n");
        _workspace.Write(".cursor/rules/notes.md", "Plain notes line.\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Rules.Select(rule => rule.Name).ShouldBe(["frontend/style", "notes"]);
        var style = set.Rules[0];
        style.Kind.ShouldBe(ConventionRuleKind.Mdc);
        style.Description.ShouldBe("Frontend style");
        style.Globs.ShouldBe(["web/**/*.ts", "web/**/*.tsx"]);
        style.AlwaysApply.ShouldBeTrue();
        set.Rules[1].Kind.ShouldBe(ConventionRuleKind.Markdown);
        set.Rules[1].Description.ShouldBe("Plain notes line.");
    }

    /// <summary>Skill frontmatter: paths, the two invocation switches, icon and colour; a name that differs from the folder is reported and the folder name is used; a folder with no SKILL.md is ignored.</summary>
    [Fact]
    public async Task SkillFrontmatterIsReadAndANameMismatchIsReported()
    {
        _workspace.Write(".cursor/skills/review/SKILL.md", "---\nname: review\ndescription: Reviews code\npaths: [src/**, tests/**]\ndisable-model-invocation: true\nuser-invocable: false\nicon: magnifier\ncolor: purple\n---\nSteps.\n");
        _workspace.Write(".cursor/skills/renamed/SKILL.md", "---\nname: something-else\ndescription: Mismatch\n---\n");
        _workspace.Write(".cursor/skills/no-skill-file/README.md", "Not a skill.\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.Skills.Select(skill => skill.Name).ShouldBe(["renamed", "review"]);
        var review = set.Skills.Single(skill => string.Equals(skill.Name, "review", StringComparison.Ordinal));
        review.Description.ShouldBe("Reviews code");
        review.Paths.ShouldBe(["src/**", "tests/**"]);
        review.DisableModelInvocation.ShouldBeTrue();
        review.UserInvocable.ShouldBeFalse();
        review.Icon.ShouldBe("magnifier");
        review.Color.ShouldBe("purple");
        var renamed = set.Skills.Single(skill => string.Equals(skill.Name, "renamed", StringComparison.Ordinal));
        renamed.UserInvocable.ShouldBeTrue("a skill is user-invocable unless its file says otherwise");
        renamed.DisableModelInvocation.ShouldBeFalse();
        var diagnostic = set.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Kind.ShouldBe(ConventionDiagnosticKind.Invalid);
        diagnostic.Reason.ShouldContain("something-else", Case.Sensitive);
    }

    /// <summary>Under .autonogrammer, a flat skills/&lt;name&gt;.md file is a skill (the earlier Autonogrammer layout); under the other directories it is not.</summary>
    [Fact]
    public async Task FlatSkillFilesCountOnlyUnderAutonogrammer()
    {
        _workspace.Write(".autonogrammer/skills/lint.md", "# Lint\nRun the linter.\n");
        _workspace.Write(".cursor/skills/flat.md", "Not a skill.\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        var skill = set.Skills.ShouldHaveSingleItem();
        skill.Name.ShouldBe("lint");
        skill.Description.ShouldBe("Run the linter.");
        skill.Origin.ShouldBe(".autonogrammer/skills/lint.md");
    }

    /// <summary>A command without frontmatter takes its description from its first body line, and a name that cannot be typed after a slash is an Invalid diagnostic.</summary>
    [Fact]
    public async Task CommandDescriptionsFallBackToTheBodyAndBadNamesAreReported()
    {
        _workspace.Write(".claude/commands/standup.md", "# Standup\n\nSummarise yesterday.\n");
        _workspace.Write(".claude/commands/has space.md", "Never typed.\n");
        _workspace.Write(".claude/commands/readme.txt", "Not a command file.\n");

        var set = await DiscoverAsync().ConfigureAwait(true);

        var command = set.Commands.ShouldHaveSingleItem();
        command.Description.ShouldBe("Summarise yesterday.");
        command.ArgumentHint.ShouldBeNull();
        set.Diagnostics.ShouldHaveSingleItem().Kind.ShouldBe(ConventionDiagnosticKind.Invalid);
    }

    /// <summary>Hooks and MCP files are listed with their path and scope and never read: the project's first (in directory order), then the user's.</summary>
    [Fact]
    public async Task HookAndMcpFilesAreListedNotParsed()
    {
        _workspace.Write(".cursor/hooks.json", "{ not even json");
        _workspace.Write(".autonogrammer/hooks.json", "{}");
        _workspace.Write(".cursor/mcp.json", "{}");
        _workspace.Write(".mcp.json", "{}");
        _home.Write(".cursor/hooks.json", "{}");
        _home.Write(".claude/mcp.json", "{}");

        var set = await DiscoverAsync().ConfigureAwait(true);

        set.HookFiles.Select(file => file.Origin).ShouldBe([".autonogrammer/hooks.json", ".cursor/hooks.json", "~/.cursor/hooks.json"]);
        set.HookFiles.Select(file => file.Scope).ShouldBe([ConventionScope.Project, ConventionScope.Project, ConventionScope.User]);
        set.McpFiles.Select(file => file.Origin).ShouldBe([".cursor/mcp.json", ".mcp.json", "~/.claude/mcp.json"]);
        set.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>A workspace with no convention directory finds nothing and reports nothing; a missing home or root never throws.</summary>
    [Fact]
    public async Task NothingToFindIsNotAProblem()
    {
        var empty = await DiscoverAsync().ConfigureAwait(true);
        var noHome = await ConventionCatalog.DiscoverAsync(
            [_workspace.RootPath],
            null,
            ConventionSources.Local([_workspace.RootPath], null),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        var missingRoot = Path.Combine(_workspace.RootPath, "gone");
        var missing = await ConventionCatalog.DiscoverAsync(
            [missingRoot],
            null,
            ConventionSources.Local([missingRoot], null),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        empty.Commands.ShouldBeEmpty();
        empty.Skills.ShouldBeEmpty();
        empty.Rules.ShouldBeEmpty();
        empty.Subagents.ShouldBeEmpty();
        empty.HookFiles.ShouldBeEmpty();
        empty.McpFiles.ShouldBeEmpty();
        empty.Diagnostics.ShouldBeEmpty();
        noHome.Diagnostics.ShouldBeEmpty();
        missing.Commands.ShouldBeEmpty();
    }

    /// <summary>A cancelled token stops discovery with the cancellation, not with a diagnostic.</summary>
    [Fact]
    public async Task CancellationIsHonoured()
    {
        _workspace.Write(".cursor/commands/x.md", "x\n");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(
            () => ConventionCatalog.DiscoverAsync([_workspace.RootPath], null, ConventionSources.Local([_workspace.RootPath], null), cancellationToken: cancelled.Token)).ConfigureAwait(true);
    }

    /// <summary>Null arguments are refused.</summary>
    [Fact]
    public async Task NullArgumentsAreRefused()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => ConventionCatalog.DiscoverAsync(null!, null, ConventionSources.Local([], null))).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentNullException>(() => ConventionCatalog.DiscoverAsync([], null, null!)).ConfigureAwait(true);
    }
}
