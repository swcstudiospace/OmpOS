using Autonogrammer.Native.Settings;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Settings.Customize;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>The Rules, Skills and Commands pages over a workspace, a home folder and the shipped command catalogue: every convention directory is read at both scopes (PX-13).</summary>
public sealed class WorkspaceCatalogSourcesTests : IDisposable
{
    private readonly HeadTempDirectory _workspace = new();
    private readonly HeadTempDirectory _home = new();

    /// <inheritdoc />
    public void Dispose()
    {
        _workspace.Dispose();
        _home.Dispose();
    }

    /// <summary>Each rule file becomes a project entry: its name, its path, and a description from its front matter or its first line of text. Entries follow the name order.</summary>
    [Fact]
    public async Task RuleFilesBecomeProjectEntriesWithDescriptions()
    {
        _ = _workspace.Write(".autonogrammer/rules/no-secrets.md", "---\ndescription: Never commit secrets\n---\n# Heading\nBody text\n");
        _ = _workspace.Write(".autonogrammer/rules/style.md", "# Style\nPrefer small functions.\n");
        var page = new RulesSettingsPage(new RulesSettingsSource(_workspace.Root, null));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(CustomizePageState.Ready);
        page.Entries.Select(static entry => entry.Title).ShouldBe(["no-secrets", "style"]);
        var first = page.Entries[0];
        first.Scope.ShouldBe(CustomizeScope.Project);
        first.Origin.ShouldBe(".autonogrammer/rules/no-secrets.md");
        first.Description.ShouldBe("Never commit secrets");
        page.Entries[1].Description.ShouldBe("Prefer small functions.");
        page.Problems.ShouldBeEmpty();
    }

    /// <summary>Rules are read from every convention directory and from the home folder: the page lists each with its file and scope, and a rule's globs and always-apply flag are part of its description.</summary>
    [Fact]
    public async Task RulesComeFromEveryConventionDirectoryAndBothScopes()
    {
        _ = _workspace.Write(".cursor/rules/ts.mdc", "---\ndescription: TypeScript style\nglobs: **/*.ts\nalwaysApply: false\n---\n");
        _ = _workspace.Write(".claude/rules/always.md", "---\ndescription: Be brief\nalwaysApply: true\n---\n");
        _ = _workspace.Write("AGENTS.md", "# Agents\nRead the docs first.\n");
        _ = _home.Write(".codex/rules/personal.md", "Personal preference.\n");
        var page = new RulesSettingsPage(new RulesSettingsSource(_workspace.Root, _home.Root));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Entries.Select(static entry => entry.Origin).ShouldBe(["AGENTS.md", ".claude/rules/always.md", ".cursor/rules/ts.mdc", "~/.codex/rules/personal.md"]);
        page.Entries.Select(static entry => entry.Scope).ShouldBe([CustomizeScope.Project, CustomizeScope.Project, CustomizeScope.Project, CustomizeScope.User]);
        page.Entries.Single(static entry => string.Equals(entry.Origin, ".cursor/rules/ts.mdc", StringComparison.Ordinal)).Description.ShouldBe("TypeScript style Applies to **/*.ts.");
        page.Entries.Single(static entry => string.Equals(entry.Origin, ".claude/rules/always.md", StringComparison.Ordinal)).Description.ShouldBe("Be brief Always applied.");
        page.Entries.Single(static entry => string.Equals(entry.Origin, "AGENTS.md", StringComparison.Ordinal)).Description.ShouldBe("Read the docs first.");
        page.Problems.ShouldBeEmpty();
    }

    /// <summary>A file that cannot be a rule is listed as a problem of the Rules page only, and the other rules still list; a flat .autonogrammer skill file is a skill.</summary>
    [Fact]
    public async Task AMalformedRuleFileIsAProblemOfTheRulesPageOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _workspace.Write(".autonogrammer/rules/good.md", "# Good\nA rule.\n");
        _ = _workspace.Write(".autonogrammer/rules/broken.mdc", "---\nglobs: [unclosed\n---\n");
        _ = _workspace.Write(".autonogrammer/skills/lint.md", "# Lint\nRun the linter.\n");
        var rules = new RulesSettingsPage(new RulesSettingsSource(_workspace.Root, null));
        var skills = new SkillsSettingsPage(new SkillsSettingsSource(_workspace.Root, null));

        await rules.LoadAsync(ct).ConfigureAwait(true);
        await skills.LoadAsync(ct).ConfigureAwait(true);

        rules.Entries.Select(static entry => entry.Title).ShouldBe(["good"]);
        var problem = rules.Problems.ShouldHaveSingleItem();
        problem.ShouldStartWith(".autonogrammer/rules/broken.mdc: ", Case.Sensitive);
        problem.ShouldContain("malformed frontmatter", Case.Sensitive);
        var workspaceSkills = skills.Entries.Where(static entry => entry.Scope != CustomizeScope.BuiltIn).ToArray();
        workspaceSkills.Select(static entry => entry.Title).ShouldBe(["lint"]);
        workspaceSkills[0].Origin.ShouldBe(".autonogrammer/skills/lint.md");
        skills.Problems.ShouldBeEmpty();
    }

    /// <summary>Skills are read from every convention directory and from the home folder; a skill that is not offered after a slash says so.</summary>
    [Fact]
    public async Task SkillsComeFromEveryConventionDirectoryAndBothScopes()
    {
        _ = _workspace.Write(".agents/skills/audit/SKILL.md", "---\nname: audit\ndescription: Audits dependencies\n---\n");
        _ = _workspace.Write(".claude/skills/quiet/SKILL.md", "---\nname: quiet\ndescription: Background only\nuser-invocable: false\n---\n");
        _ = _home.Write(".cursor/skills/daily/SKILL.md", "---\nname: daily\ndescription: Daily notes\n---\n");
        var page = new SkillsSettingsPage(new SkillsSettingsSource(_workspace.Root, _home.Root));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var found = page.Entries.Where(static entry => entry.Scope != CustomizeScope.BuiltIn).ToArray();
        found.Select(static entry => entry.Title).ShouldBe(["audit", "quiet", "daily"]);
        found.Select(static entry => entry.Origin).ShouldBe([".agents/skills/audit/SKILL.md", ".claude/skills/quiet/SKILL.md", "~/.cursor/skills/daily/SKILL.md"]);
        found[1].Description.ShouldBe("Background only Not offered after a slash.");
        found[2].Scope.ShouldBe(CustomizeScope.User);
        page.Problems.ShouldBeEmpty();
    }

    /// <summary>A file larger than the convention allows is a problem sentence, not a failure of the page.</summary>
    [Fact]
    public async Task AFileThatIsTooLargeIsAProblemSentence()
    {
        _ = _workspace.Write(".autonogrammer/skills/huge.md", new string('x', (256 * 1024) + 1));
        var page = new SkillsSettingsPage(new SkillsSettingsSource(_workspace.Root, null));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Entries.ShouldAllBe(static entry => entry.Scope == CustomizeScope.BuiltIn, "only the skills that ship with the app list; the oversize file is no skill");
        page.Problems.ShouldHaveSingleItem().ShouldStartWith(".autonogrammer/skills/huge.md: ", Case.Sensitive);
    }

    /// <summary>A workspace with no convention folder lists nothing and reports no problem.</summary>
    [Fact]
    public async Task ANewWorkspaceIsEmpty()
    {
        var page = new RulesSettingsPage(new RulesSettingsSource(_workspace.Root, _home.Root));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(CustomizePageState.Empty);
        page.Problems.ShouldBeEmpty();
    }

    /// <summary>A workspace that does not exist is the page's error state with a plain sentence.</summary>
    [Fact]
    public async Task AMissingWorkspaceIsTheErrorState()
    {
        var page = new SkillsSettingsPage(new SkillsSettingsSource(_workspace.PathOf("gone"), null));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(CustomizePageState.Error);
        page.LoadFailure.ShouldBe("The workspace files could not be read.");
    }

    /// <summary>The Commands page lists the shipped slash commands, then the custom commands and subagents found in the workspace and the home folder, each with its file and scope.</summary>
    [Fact]
    public async Task TheCommandsPageListsTheCatalogueAndTheDiscoveredCommandsAndSubagents()
    {
        _ = _workspace.Write(".cursor/commands/deploy.md", "---\ndescription: Deploy the app\nargument-hint: <env>\n---\nDeploy to $1.\n");
        _ = _home.Write(".claude/agents/verifier.md", "---\nname: verifier\ndescription: Verifies work\n---\nCheck.\n");
        _ = _workspace.Write(".cursor/commands/bad name.md", "x\n");
        var page = new CommandsSettingsPage(new SlashCommandsSettingsSource(SlashCommandCatalogue.Shipped, new ConventionsCommandsSource(_workspace.Root, _home.Root)));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(CustomizePageState.Ready);
        page.Entries.Count.ShouldBe(SlashCommandCatalogue.Shipped.ListAll().Count + 2);
        var deploy = page.Entries.Single(static entry => entry.Title.StartsWith("/deploy", StringComparison.Ordinal));
        deploy.Title.ShouldBe("/deploy <env>");
        deploy.Scope.ShouldBe(CustomizeScope.Project);
        deploy.Origin.ShouldBe(".cursor/commands/deploy.md");
        var verifier = page.Entries.Single(static entry => entry.Title.StartsWith("/verifier", StringComparison.Ordinal));
        verifier.Scope.ShouldBe(CustomizeScope.User);
        verifier.Origin.ShouldBe("~/.claude/agents/verifier.md");
        page.Entries.Count(static entry => entry.Scope == CustomizeScope.BuiltIn).ShouldBe(SlashCommandCatalogue.Shipped.ListAll().Count);
        page.Problems.ShouldHaveSingleItem().ShouldStartWith(".cursor/commands/bad name.md: ", Case.Sensitive);
    }

    /// <summary>Without a discovery source the Commands page lists the shipped catalogue as it always did.</summary>
    [Fact]
    public async Task TheCommandsPageListsTheShippedCatalogue()
    {
        var page = new CommandsSettingsPage(new SlashCommandsSettingsSource(SlashCommandCatalogue.Shipped));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(CustomizePageState.Ready);
        page.Entries.Count.ShouldBe(SlashCommandCatalogue.Shipped.ListAll().Count);
        page.Entries.ShouldAllBe(entry => entry.Scope == CustomizeScope.BuiltIn && entry.Title.StartsWith('/'));
    }
}
