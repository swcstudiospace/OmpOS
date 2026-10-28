using OmpOS.Native.Tests.Settings.Head;
using OmpOS.Native.Views.Conventions;
using OmpOS.Presentation.Commands;
using OmpOS.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Conventions;

/// <summary>Discovery as the desktop head uses it: the slash source over a real workspace, and the mapping to the palette and the Settings pages.</summary>
public sealed class ConventionMappingTests : IDisposable
{
    private readonly HeadTempDirectory _workspace = new();
    private readonly HeadTempDirectory _home = new();

    /// <inheritdoc />
    public void Dispose()
    {
        _workspace.Dispose();
        _home.Dispose();
    }

    /// <summary>The slash source reads commands, skills and subagents from the workspace and the home folder and maps scopes, paths and flags; the problems are the commands' and subagents' diagnostics.</summary>
    [Fact]
    public async Task TheSlashSourceReadsEveryKindFromBothScopes()
    {
        _ = _workspace.Write(".cursor/commands/deploy.md", "---\ndescription: Deploy\nargument-hint: <env>\n---\nDeploy $1\n");
        _ = _workspace.Write(".agents/skills/audit/SKILL.md", "---\ndescription: Audit\nuser-invocable: false\n---\n");
        _ = _home.Write(".claude/agents/verifier.md", "---\nname: verifier\ndescription: Verifies\nmodel: fast\nreadonly: true\n---\nCheck.\n");
        _ = _workspace.Write(".cursor/commands/has space.md", "x\n");
        var source = new ConventionSlashDiscoverySource([_workspace.Root], _home.Root);

        var discovery = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var command = discovery.Commands.ShouldHaveSingleItem();
        command.ShouldBe(new SlashDiscoveredCommand("deploy", "Deploy", "<env>", "Deploy $1\n", SlashScope.Project, ".cursor/commands/deploy.md"));
        var skill = discovery.Skills.ShouldHaveSingleItem();
        skill.Name.ShouldBe("audit");
        skill.UserInvocable.ShouldBeFalse();
        skill.Path.ShouldEndWith("SKILL.md", Case.Sensitive);
        skill.Scope.ShouldBe(SlashScope.Project);
        var agent = discovery.Subagents.ShouldHaveSingleItem();
        agent.Scope.ShouldBe(SlashScope.User);
        agent.Model.ShouldBe("fast");
        agent.ReadOnly.ShouldBeTrue();
        agent.Instructions.ShouldBe("Check.\n");
        discovery.Problems.ShouldHaveSingleItem().ShouldStartWith(".cursor/commands/has space.md: ", Case.Sensitive);
    }

    /// <summary>Reading again picks up a file added meanwhile, which is the manual refresh.</summary>
    [Fact]
    public async Task EachLoadReadsAgain()
    {
        var source = new ConventionSlashDiscoverySource([_workspace.Root], null);
        var token = TestContext.Current.CancellationToken;

        (await source.LoadAsync(token).ConfigureAwait(true)).Commands.ShouldBeEmpty();
        _ = _workspace.Write(".ompos/commands/late.md", "Late.\n");

        (await source.LoadAsync(token).ConfigureAwait(true)).Commands.ShouldHaveSingleItem().Name.ShouldBe("late");
    }

    /// <summary>The mapping gives each Settings page only its own problems.</summary>
    [Fact]
    public void EachPageGetsItsOwnProblems()
    {
        var set = ConventionSet.Empty with
        {
            Diagnostics =
            [
                new ConventionDiagnostic(ConventionItemKind.Rule, ".cursor/rules/a.mdc", ConventionDiagnosticKind.Malformed, "bad rule"),
                new ConventionDiagnostic(ConventionItemKind.Skill, ".cursor/skills/b/SKILL.md", ConventionDiagnosticKind.TooLarge, "big skill"),
                new ConventionDiagnostic(ConventionItemKind.Command, ".cursor/commands/c.md", ConventionDiagnosticKind.Invalid, "bad command"),
                new ConventionDiagnostic(ConventionItemKind.Config, ".cursor", ConventionDiagnosticKind.EscapesRoot, "folder refused"),
            ],
        };

        ConventionMapping.ToRulesCatalog(set).Problems.ShouldBe([".cursor/rules/a.mdc: bad rule"]);
        ConventionMapping.ToSkillsCatalog(set).Problems.ShouldBe([".cursor/skills/b/SKILL.md: big skill"]);
        ConventionMapping.ToDiscovery(set).Problems.ShouldBe([".cursor/commands/c.md: bad command", ".cursor: folder refused"]);
    }

    /// <summary>Null input is refused.</summary>
    [Fact]
    public void NullArgumentsAreRefused()
    {
        Should.Throw<ArgumentNullException>(() => ConventionMapping.ToDiscovery(null!));
        Should.Throw<ArgumentNullException>(() => ConventionMapping.ToRulesCatalog(null!));
        Should.Throw<ArgumentNullException>(() => ConventionMapping.ToSkillsCatalog(null!));
        Should.Throw<ArgumentNullException>(() => new ConventionSlashDiscoverySource(null!, null));
    }
}
