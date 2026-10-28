using OmpOS.Workspace.BuiltinSkills;
using OmpOS.Workspace.Conventions;
using OmpOS.Workspace.Tests.Conventions;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests.BuiltinSkills;

/// <summary>The bundled skills join discovery as the lowest-precedence scope: they are found, they lose to every project and user skill of the same name, and the loss is reported.</summary>
public sealed class BuiltinSkillDiscoveryTests
{
    private const string Review = "---\nname: review\ndescription: Built-in review.\n---\nSteps.\n";

    private static Task<ConventionSet> DiscoverAsync(IConventionFileSource project, IConventionFileSource user, BuiltinSkillSource? builtin) =>
        ConventionCatalog.DiscoverAsync(["/ws"], "/home/dev", new ConventionSources(project, user, builtin), cancellationToken: TestContext.Current.CancellationToken);

    /// <summary>Every skill folder of the bundle is a skill of the built-in scope, with an origin that says where it came from.</summary>
    [Fact]
    public async Task BundledSkillsAreFoundInTheBuiltinScope()
    {
        using var bundle = new TempWorkspace();
        bundle.Write("skills/review/SKILL.md", Review);
        bundle.Write("skills/audit/SKILL.md", "Audit it.\n");

        var set = await DiscoverAsync(new InMemoryConventionFileSource(), new InMemoryConventionFileSource(), new BuiltinSkillSource(bundle.RootPath)).ConfigureAwait(true);

        set.Skills.Select(skill => (skill.Name, skill.Scope, skill.ScopeDirectory, skill.SourceDirectory, skill.Origin)).ShouldBe(
        [
            ("audit", ConventionScope.Builtin, string.Empty, string.Empty, "builtin:skills/audit/SKILL.md"),
            ("review", ConventionScope.Builtin, string.Empty, string.Empty, "builtin:skills/review/SKILL.md"),
        ]);
        set.Skills[1].Description.ShouldBe("Built-in review.");
        set.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>A project skill of the same name wins, in any convention folder, and the built-in one is reported as the loser.</summary>
    [Fact]
    public async Task AProjectSkillShadowsTheBuiltinOne()
    {
        using var bundle = new TempWorkspace();
        bundle.Write("skills/review/SKILL.md", Review);
        var project = new InMemoryConventionFileSource();
        project.Add("/ws/.cursor/skills/review/SKILL.md", "---\ndescription: Our review.\n---\n");

        var set = await DiscoverAsync(project, new InMemoryConventionFileSource(), new BuiltinSkillSource(bundle.RootPath)).ConfigureAwait(true);

        var winner = set.Skills.ShouldHaveSingleItem();
        winner.Scope.ShouldBe(ConventionScope.Project);
        winner.Description.ShouldBe("Our review.");
        var note = set.Diagnostics.ShouldHaveSingleItem();
        note.Kind.ShouldBe(ConventionDiagnosticKind.Duplicate);
        note.Item.ShouldBe(ConventionItemKind.Skill);
        note.Origin.ShouldBe("builtin:skills/review/SKILL.md");
        note.Reason.ShouldContain(".cursor/skills/review/SKILL.md", Case.Sensitive);
    }

    /// <summary>A user skill of the same name also wins over the built-in one.</summary>
    [Fact]
    public async Task AUserSkillShadowsTheBuiltinOne()
    {
        using var bundle = new TempWorkspace();
        bundle.Write("skills/review/SKILL.md", Review);
        var user = new InMemoryConventionFileSource();
        user.Add("/home/dev/.agents/skills/review/SKILL.md", "---\ndescription: My review.\n---\n");

        var set = await DiscoverAsync(new InMemoryConventionFileSource(), user, new BuiltinSkillSource(bundle.RootPath)).ConfigureAwait(true);

        var winner = set.Skills.ShouldHaveSingleItem();
        winner.Scope.ShouldBe(ConventionScope.User);
        winner.Origin.ShouldBe("~/.agents/skills/review/SKILL.md");
        set.Diagnostics.ShouldHaveSingleItem().Reason.ShouldContain("~/.agents/skills/review/SKILL.md", Case.Sensitive);
    }

    /// <summary>With a project skill, a user skill and a built-in one of the same name the project skill wins, and both others are reported.</summary>
    [Fact]
    public async Task ProjectBeatsUserBeatsBuiltin()
    {
        using var bundle = new TempWorkspace();
        bundle.Write("skills/review/SKILL.md", Review);
        var project = new InMemoryConventionFileSource();
        project.Add("/ws/.ompos/skills/review/SKILL.md", "Project review.\n");
        var user = new InMemoryConventionFileSource();
        user.Add("/home/dev/.ompos/skills/review/SKILL.md", "User review.\n");

        var set = await DiscoverAsync(project, user, new BuiltinSkillSource(bundle.RootPath)).ConfigureAwait(true);

        set.Skills.ShouldHaveSingleItem().Scope.ShouldBe(ConventionScope.Project);
        set.Diagnostics.Select(note => note.Origin).Order(StringComparer.Ordinal).ShouldBe(["builtin:skills/review/SKILL.md", "~/.ompos/skills/review/SKILL.md"]);
    }

    /// <summary>Skills of the built-in scope are listed after the project and user skills, whatever their names.</summary>
    [Fact]
    public async Task BuiltinSkillsAreListedAfterUserSkills()
    {
        using var bundle = new TempWorkspace();
        bundle.Write("skills/alpha/SKILL.md", "Alpha.\n");
        var project = new InMemoryConventionFileSource();
        project.Add("/ws/.ompos/skills/zeta/SKILL.md", "Zeta.\n");

        var set = await DiscoverAsync(project, new InMemoryConventionFileSource(), new BuiltinSkillSource(bundle.RootPath)).ConfigureAwait(true);

        set.Skills.Select(skill => (skill.Name, skill.Scope)).ShouldBe([("zeta", ConventionScope.Project), ("alpha", ConventionScope.Builtin)]);
    }

    /// <summary>Sources without a built-in part find no built-in skill, even when a bundle exists on disk.</summary>
    [Fact]
    public async Task WithoutABuiltinSourceNothingBuiltinIsFound()
    {
        using var bundle = new TempWorkspace();
        bundle.Write("skills/review/SKILL.md", Review);

        var set = await DiscoverAsync(new InMemoryConventionFileSource(), new InMemoryConventionFileSource(), builtin: null).ConfigureAwait(true);

        set.Skills.ShouldBeEmpty();
        set.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>A missing bundle folder is not an error: no skills and no diagnostic.</summary>
    [Fact]
    public async Task AMissingBundleFolderIsNotAnError()
    {
        using var bundle = new TempWorkspace();

        var set = await DiscoverAsync(new InMemoryConventionFileSource(), new InMemoryConventionFileSource(), new BuiltinSkillSource(Path.Combine(bundle.RootPath, "absent"))).ConfigureAwait(true);

        set.Skills.ShouldBeEmpty();
        set.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>A malformed built-in skill is a diagnostic with a <c>builtin:</c> origin and the other bundled skills still load.</summary>
    [Fact]
    public async Task AMalformedBundledSkillIsADiagnostic()
    {
        using var bundle = new TempWorkspace();
        bundle.Write("skills/broken/SKILL.md", "---\nname: broken\n");
        bundle.Write("skills/review/SKILL.md", Review);

        var set = await DiscoverAsync(new InMemoryConventionFileSource(), new InMemoryConventionFileSource(), new BuiltinSkillSource(bundle.RootPath)).ConfigureAwait(true);

        set.Skills.ShouldHaveSingleItem().Name.ShouldBe("review");
        var problem = set.Diagnostics.ShouldHaveSingleItem();
        problem.Kind.ShouldBe(ConventionDiagnosticKind.Malformed);
        problem.Origin.ShouldBe("builtin:skills/broken/SKILL.md");
    }

    /// <summary>The frontmatter flags of a bundled skill are read like those of any other skill.</summary>
    [Fact]
    public async Task BundledSkillFlagsAreRead()
    {
        using var bundle = new TempWorkspace();
        bundle.Write("skills/manual/SKILL.md", "---\ndisable-model-invocation: true\nuser-invocable: false\n---\nBody.\n");

        var set = await DiscoverAsync(new InMemoryConventionFileSource(), new InMemoryConventionFileSource(), new BuiltinSkillSource(bundle.RootPath)).ConfigureAwait(true);

        var skill = set.Skills.ShouldHaveSingleItem();
        skill.DisableModelInvocation.ShouldBeTrue();
        skill.UserInvocable.ShouldBeFalse();
    }

    /// <summary><see cref="ConventionSources.WithBuiltin"/> keeps the project and user sources and adds the bundle.</summary>
    [Fact]
    public void WithBuiltinKeepsTheOtherSources()
    {
        var project = new InMemoryConventionFileSource();
        var user = new InMemoryConventionFileSource();
        using var bundle = new TempWorkspace();
        var builtin = new BuiltinSkillSource(bundle.RootPath);

        var sources = new ConventionSources(project, user).WithBuiltin(builtin);

        sources.Project.ShouldBeSameAs(project);
        sources.User.ShouldBeSameAs(user);
        sources.Builtin.ShouldBeSameAs(builtin);
    }
}
