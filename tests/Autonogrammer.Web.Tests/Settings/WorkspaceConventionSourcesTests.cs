using System.Net;
using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Customize;
using Autonogrammer.Web.Services.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests.Settings;

/// <summary>The Rules, Skills and slash palette sources over <c>GET /workspace/conventions</c>.</summary>
public sealed class WorkspaceConventionSourcesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ConventionRuleInfo Rule(string name, ConventionRuleType type, string description, bool alwaysApply, string[] globs, ConventionScopeKind scope = ConventionScopeKind.Project) => new()
    {
        Name = name,
        Type = type,
        Description = description,
        Globs = globs,
        AlwaysApply = alwaysApply,
        ScopeDirectory = string.Empty,
        Scope = scope,
        SourceDirectory = ".cursor",
        DirectoryRank = 2,
        Origin = scope == ConventionScopeKind.User ? "~/.cursor/rules/" + name + ".mdc" : ".cursor/rules/" + name + ".mdc",
    };

    private static ConventionSkillInfo Skill(string name, bool userInvocable, bool disableModel) => new()
    {
        Name = name,
        Description = "Skill " + name,
        Paths = [],
        DisableModelInvocation = disableModel,
        UserInvocable = userInvocable,
        ScopeDirectory = string.Empty,
        Scope = ConventionScopeKind.Project,
        SourceDirectory = ".claude",
        DirectoryRank = 3,
        Origin = ".claude/skills/" + name + "/SKILL.md",
    };

    /// <summary>Rules map to entries with the scope, the origin and a description that names always-applied and glob-scoped rules; only rule problems appear.</summary>
    [Fact]
    public async Task Rules_MapToEntries_AndOnlyRuleProblemsAppear()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = WorkspaceFixtures.Conventions(
            rules: [Rule("style", ConventionRuleType.Mdc, "House style", true, ["src/**"]), Rule("mine", ConventionRuleType.Markdown, string.Empty, false, [], ConventionScopeKind.User)],
            diagnostics:
            [
                WorkspaceFixtures.Diagnostic(ConventionItemType.Rule, ".cursor/rules/bad.mdc", "the frontmatter is malformed"),
                WorkspaceFixtures.Diagnostic(ConventionItemType.Command, ".cursor/commands/bad.md", "not a rule problem"),
            ]);
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.ConventionsResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceRulesSettingsSource(WorkspaceFixtures.Client(http));

        var catalog = await source.LoadAsync(Token).ConfigureAwait(true);

        catalog.Entries.Select(static entry => entry.Title).ShouldBe(["style", "mine"]);
        catalog.Entries[0].Scope.ShouldBe(CustomizeScope.Project);
        catalog.Entries[0].Origin.ShouldBe(".cursor/rules/style.mdc");
        catalog.Entries[0].Description.ShouldBe("House style Always applied. Applies to src/**.");
        catalog.Entries[1].Scope.ShouldBe(CustomizeScope.User);
        catalog.Entries[1].Description.ShouldBe(string.Empty);
        catalog.Problems.ShouldBe([".cursor/rules/bad.mdc: the frontmatter is malformed"]);
    }

    /// <summary>Skills map to entries and say when a skill is hidden from the slash palette or from the model.</summary>
    [Fact]
    public async Task Skills_MapToEntries_WithTheirInvocationNotes()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = WorkspaceFixtures.Conventions(skills: [Skill("lint", true, false), Skill("quiet", false, true)]);
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.ConventionsResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceSkillsSettingsSource(WorkspaceFixtures.Client(http));

        var catalog = await source.LoadAsync(Token).ConfigureAwait(true);

        catalog.Entries[0].Description.ShouldBe("Skill lint");
        catalog.Entries[1].Description.ShouldBe("Skill quiet Not offered after a slash. The model does not pick it by itself.");
        catalog.Problems.ShouldBeEmpty();
    }

    /// <summary>A workspace the API cannot read is the page's error with the API's own reason.</summary>
    [Fact]
    public async Task AnUnavailableWorkspace_IsTheErrorStateWithTheReason()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = WorkspaceFixtures.Conventions() with { Available = false, Reason = "The API has no workspace folder." };
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.ConventionsResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceRulesSettingsSource(WorkspaceFixtures.Client(http));

        var failure = await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldBe("The API has no workspace folder.");
    }

    /// <summary>A refused call becomes a sentence for the page and never quotes the response.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Sign in again")]
    [InlineData(HttpStatusCode.NotFound, "not available to your account")]
    [InlineData(HttpStatusCode.TooManyRequests, "Too many requests")]
    [InlineData(HttpStatusCode.BadGateway, "status 502")]
    public async Task ARefusedCall_IsAPlainSentence(HttpStatusCode status, string expected)
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        using var refused = WorkspaceFixtures.Text(status, "private detail");
        handler.Enqueue(refused);
        var source = new WorkspaceSkillsSettingsSource(WorkspaceFixtures.Client(http));

        var failure = await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldContain(expected, Case.Sensitive);
        failure.Message.ShouldNotContain("private detail", Case.Sensitive);
    }

    /// <summary>A network fault is the same kind of failure: the page shows its error state, no exception escapes.</summary>
    [Fact]
    public async Task ANetworkFault_IsTheErrorState()
    {
        using var handler = new FaultingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
        var source = new WorkspaceRulesSettingsSource(WorkspaceFixtures.Client(http));

        var failure = await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("could not be reached", Case.Sensitive);
    }

    /// <summary>The slash palette gets the commands, skills and subagents with the origin as the skill path, and only command, subagent and config problems.</summary>
    [Fact]
    public async Task SlashDiscovery_MapsCommandsSkillsAndSubagents()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var agent = new ConventionSubagentInfo
        {
            Name = "reviewer",
            Description = "Reviews",
            Model = "inherit",
            ReadOnly = true,
            IsBackground = false,
            Instructions = "Review carefully",
            Scope = ConventionScopeKind.User,
            SourceDirectory = ".codex",
            DirectoryRank = 4,
            Origin = "~/.codex/agents/reviewer.md",
        };
        var answer = WorkspaceFixtures.Conventions(
            commands: [WorkspaceFixtures.Command("ship"), WorkspaceFixtures.Command("home", ConventionScopeKind.User, "~/.cursor/commands/home.md")],
            skills: [Skill("lint", true, false)],
            subagents: [agent],
            diagnostics:
            [
                WorkspaceFixtures.Diagnostic(ConventionItemType.Command, ".cursor/commands/bad.md", "bad command"),
                WorkspaceFixtures.Diagnostic(ConventionItemType.Config, ".cursor", "cannot read"),
                WorkspaceFixtures.Diagnostic(ConventionItemType.Skill, ".claude/skills/x/SKILL.md", "bad skill"),
            ]);
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.ConventionsResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceSlashDiscoverySource(WorkspaceFixtures.Client(http));

        var discovery = await source.LoadAsync(Token).ConfigureAwait(true);

        discovery.Commands.Select(static command => command.Name).ShouldBe(["ship", "home"]);
        var ship = discovery.Commands[0];
        ship.Description.ShouldBe("Does ship");
        ship.ArgumentHint.ShouldBe("<arg>");
        ship.Template.ShouldBe("Run ship $ARGUMENTS");
        ship.Scope.ShouldBe(SlashScope.Project);
        discovery.Commands[1].Scope.ShouldBe(SlashScope.User);
        var skill = discovery.Skills.ShouldHaveSingleItem();
        skill.Path.ShouldBe(".claude/skills/lint/SKILL.md");
        skill.Origin.ShouldBe(".claude/skills/lint/SKILL.md");
        skill.UserInvocable.ShouldBeTrue();
        var subagent = discovery.Subagents.ShouldHaveSingleItem();
        subagent.Scope.ShouldBe(SlashScope.User);
        subagent.ReadOnly.ShouldBeTrue();
        subagent.Instructions.ShouldBe("Review carefully");
        discovery.Problems.ShouldBe([".cursor/commands/bad.md: bad command", ".cursor: cannot read"]);
    }

    /// <summary>When the API cannot be read the palette keeps working: the failure is the one problem of an empty discovery, not an exception.</summary>
    [Fact]
    public async Task SlashDiscovery_OnAFailure_IsAnEmptyDiscoveryWithOneProblem()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        using var refused = WorkspaceFixtures.Text(HttpStatusCode.Unauthorized, string.Empty);
        handler.Enqueue(refused);
        var source = new WorkspaceSlashDiscoverySource(WorkspaceFixtures.Client(http));

        var discovery = await source.LoadAsync(Token).ConfigureAwait(true);

        discovery.Commands.ShouldBeEmpty();
        discovery.Skills.ShouldBeEmpty();
        discovery.Subagents.ShouldBeEmpty();
        discovery.Problems.ShouldHaveSingleItem().ShouldContain("Sign in again", Case.Sensitive);
    }

    /// <summary>Each call reads again, so calling it is the manual refresh.</summary>
    [Fact]
    public async Task SlashDiscovery_ReadsAgainOnEveryCall()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        using var first = WorkspaceFixtures.Json(HttpStatusCode.OK, WorkspaceFixtures.Conventions(commands: [WorkspaceFixtures.Command("one")]), WorkspaceSettingsJsonContext.Default.ConventionsResponse);
        using var second = WorkspaceFixtures.Json(HttpStatusCode.OK, WorkspaceFixtures.Conventions(commands: [WorkspaceFixtures.Command("two")]), WorkspaceSettingsJsonContext.Default.ConventionsResponse);
        handler.Enqueue(first);
        handler.Enqueue(second);
        var source = new WorkspaceSlashDiscoverySource(WorkspaceFixtures.Client(http));

        var one = await source.LoadAsync(Token).ConfigureAwait(true);
        var two = await source.LoadAsync(Token).ConfigureAwait(true);

        one.Commands.ShouldHaveSingleItem().Name.ShouldBe("one");
        two.Commands.ShouldHaveSingleItem().Name.ShouldBe("two");
        handler.Requests.Count.ShouldBe(2);
    }
}
