using OmpOS.Native.Settings;
using OmpOS.Presentation.Settings.Customize;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>The Rules, Skills and Commands pages over a workspace and the shipped command catalogue.</summary>
public sealed class WorkspaceCatalogSourcesTests : IDisposable
{
    private readonly HeadTempDirectory _workspace = new();

    /// <inheritdoc />
    public void Dispose() => _workspace.Dispose();

    /// <summary>Each rule file becomes a project entry: its id, its path, and a description from its front matter or its first line of text. Entries follow the id order.</summary>
    [Fact]
    public async Task RuleFilesBecomeProjectEntriesWithDescriptions()
    {
        _ = _workspace.Write(".ompos/rules/no-secrets.md", "---\ndescription: Never commit secrets\n---\n# Heading\nBody text\n");
        _ = _workspace.Write(".ompos/rules/style.md", "# Style\nPrefer small functions.\n");
        var page = new RulesSettingsPage(new RulesSettingsSource(_workspace.Root));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(CustomizePageState.Ready);
        page.Entries.Select(static entry => entry.Title).ShouldBe(["no-secrets", "style"]);
        var first = page.Entries[0];
        first.Scope.ShouldBe(CustomizeScope.Project);
        first.Origin.ShouldBe(".ompos/rules/no-secrets.md");
        first.Description.ShouldBe("Never commit secrets");
        page.Entries[1].Description.ShouldBe("Prefer small functions.");
        page.Problems.ShouldBeEmpty();
    }

    /// <summary>A file that cannot be a rule is listed as a problem of the Rules page only, and the other rules still list.</summary>
    [Fact]
    public async Task AMalformedRuleFileIsAProblemOfTheRulesPageOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _workspace.Write(".ompos/rules/good.md", "# Good\nA rule.\n");
        _ = _workspace.Write(".ompos/rules/Bad Name.md", "# Bad\nNot a valid id.\n");
        _ = _workspace.Write(".ompos/skills/lint.md", "# Lint\nRun the linter.\n");
        var rules = new RulesSettingsPage(new RulesSettingsSource(_workspace.Root));
        var skills = new SkillsSettingsPage(new SkillsSettingsSource(_workspace.Root));

        await rules.LoadAsync(ct).ConfigureAwait(true);
        await skills.LoadAsync(ct).ConfigureAwait(true);

        rules.Entries.Select(static entry => entry.Title).ShouldBe(["good"]);
        var problem = rules.Problems.ShouldHaveSingleItem();
        problem.ShouldStartWith(".ompos/rules/Bad Name.md: ", Case.Sensitive);
        problem.ShouldContain("not a valid id", Case.Sensitive);
        skills.Entries.Select(static entry => entry.Title).ShouldBe(["lint"]);
        skills.Entries[0].Origin.ShouldBe(".ompos/skills/lint.md");
        skills.Problems.ShouldBeEmpty();
    }

    /// <summary>A file larger than the convention allows is a problem sentence, not a failure of the page.</summary>
    [Fact]
    public async Task AFileThatIsTooLargeIsAProblemSentence()
    {
        _ = _workspace.Write(".ompos/skills/huge.md", new string('x', (256 * 1024) + 1));
        var page = new SkillsSettingsPage(new SkillsSettingsSource(_workspace.Root));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Entries.ShouldBeEmpty();
        page.Problems.ShouldHaveSingleItem().ShouldStartWith(".ompos/skills/huge.md: ", Case.Sensitive);
    }

    /// <summary>A workspace with no convention folder lists nothing and reports no problem.</summary>
    [Fact]
    public async Task ANewWorkspaceIsEmpty()
    {
        var page = new RulesSettingsPage(new RulesSettingsSource(_workspace.Root));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(CustomizePageState.Empty);
        page.Problems.ShouldBeEmpty();
    }

    /// <summary>A workspace that does not exist is the page's error state with a plain sentence.</summary>
    [Fact]
    public async Task AMissingWorkspaceIsTheErrorState()
    {
        var page = new SkillsSettingsPage(new SkillsSettingsSource(_workspace.PathOf("gone")));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(CustomizePageState.Error);
        page.LoadFailure.ShouldBe("The workspace files could not be read.");
    }

    /// <summary>The Commands page lists the shipped slash commands.</summary>
    [Fact]
    public async Task TheCommandsPageListsTheShippedCatalogue()
    {
        var page = new CommandsSettingsPage(new SlashCommandsSettingsSource(OmpOS.Presentation.Commands.SlashCommandCatalogue.Shipped));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(CustomizePageState.Ready);
        page.Entries.Count.ShouldBe(OmpOS.Presentation.Commands.SlashCommandCatalogue.Shipped.ListAll().Count);
        page.Entries.ShouldAllBe(entry => entry.Scope == CustomizeScope.BuiltIn && entry.Title.StartsWith('/'));
    }
}
