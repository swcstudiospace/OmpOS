using Autonogrammer.Presentation.Commands;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Commands;

/// <summary>The resolver: precedence between the catalogue, the engine and the discovered conventions, ambiguity, refusals, and the fall-through of unknown names (PX-12).</summary>
public sealed class SlashCommandResolverTests
{
    private static readonly SlashCommandResolver Resolver = new(SlashCommandCatalogue.Shipped);

    private static SlashResolutionContext Context(
        IReadOnlyList<SlashEngineCommand>? engine = null,
        SlashDiscovery? discovery = null,
        ISlashActionHost? host = null) => new(engine ?? [], discovery ?? SlashDiscovery.Empty, host);

    private static SlashDiscoveredCommand Command(string name, string template, SlashScope scope = SlashScope.Project, string? hint = null) =>
        new(name, "A command.", hint, template, scope, $".cursor/commands/{name}.md");

    private static SlashDiscoveredSkill Skill(string name, SlashScope scope = SlashScope.Project, bool userInvocable = true) =>
        new(name, "A skill.", userInvocable, $"/work/.cursor/skills/{name}/SKILL.md", scope, $".cursor/skills/{name}/SKILL.md");

    private static SlashDiscoveredSubagent Subagent(string name) =>
        new(name, "Verifies work.", "inherit", ReadOnly: true, IsBackground: false, "Check the claims.", SlashScope.Project, $".cursor/agents/{name}.md");

    /// <summary>Text that does not start with a slash command, or whose name no source knows, passes through exactly as typed, so omp sees what the user typed.</summary>
    [Theory]
    [InlineData("hello there")]
    [InlineData("/")]
    [InlineData("/ ")]
    [InlineData("/usr/bin/env ls")]
    [InlineData("/definitely-not-a-command arg one")]
    [InlineData("& ")]
    [InlineData("&&")]
    public void UnknownTextPassesThroughUnchanged(string input)
    {
        var resolution = Resolver.Resolve(input, Context());

        resolution.ShouldBe(new SlashPassThrough(input));
    }

    /// <summary>A command that omp handles is sent as typed, not rewritten, whether the catalogue delegates it or only the engine advertises it.</summary>
    [Fact]
    public void ADelegatedCommandIsSentAsTyped()
    {
        var resolution = Resolver.Resolve("/rename My title", Context());

        var send = resolution.ShouldBeOfType<SlashSendPrompt>();
        send.Prompt.ShouldBe("/rename My title");
        send.Chosen.Source.ShouldBe(SlashPaletteSource.Autonogrammer);
        send.Alternatives.ShouldBeEmpty();
    }

    /// <summary>A Cursor alias that omp names differently is rewritten to omp's command, keeping the arguments.</summary>
    [Theory]
    [InlineData("/summarize", "/compact")]
    [InlineData("/compress focus on the API", "/compact focus on the API")]
    [InlineData("/compact soft", "/compact soft")]
    [InlineData("/worktree feature-x", "/worktree feature-x")]
    public void AnAliasIsRewrittenToTheOmpCommand(string typed, string sent)
    {
        var resolution = Resolver.Resolve(typed, Context());

        resolution.ShouldBeOfType<SlashSendPrompt>().Prompt.ShouldBe(sent);
    }

    /// <summary>When the engine advertises the very command the catalogue delegates, the engine is the one that runs it and the name is not ambiguous.</summary>
    [Fact]
    public void TheEngineStandsForADelegatedCatalogueRowOfTheSameName()
    {
        var resolution = Resolver.Resolve("/fast on", Context([new SlashEngineCommand("fast", "Toggle fast mode", "[on|off]")]));

        var send = resolution.ShouldBeOfType<SlashSendPrompt>();
        send.Prompt.ShouldBe("/fast on");
        send.Chosen.Source.ShouldBe(SlashPaletteSource.Engine);
        send.Alternatives.ShouldBeEmpty();
    }

    /// <summary>A native action runs through the host when the host can run it, and its request carries the catalogue's argument and the typed text.</summary>
    [Fact]
    public void ANativeActionRunsThroughTheHost()
    {
        var resolution = Resolver.Resolve("/plan  add a cache ", Context(host: new FixedHost(SlashActionIds.ModeSet)));

        var run = resolution.ShouldBeOfType<SlashRunAction>();
        run.Request.ShouldBe(new SlashActionRequest(SlashActionIds.ModeSet, "plan", "add a cache"));
        run.Command.Name.ShouldBe("plan");
    }

    /// <summary>A native action the window cannot run, or no host at all, is refused with the reason, and nothing is sent.</summary>
    [Fact]
    public void ANativeActionTheWindowCannotRunIsRefused()
    {
        Resolver.Resolve("/plan x", Context(host: new FixedHost()))
            .ShouldBe(new SlashRefused("plan", SlashPaletteCopy.ActionNotInThisWindow));
        Resolver.Resolve("/plan x", Context())
            .ShouldBe(new SlashRefused("plan", SlashPaletteCopy.ActionNotInThisWindow));
    }

    /// <summary>A token Autonogrammer cannot run is refused with its reason.</summary>
    [Fact]
    public void AnUnavailableTokenIsRefusedWithItsReason()
    {
        var resolution = Resolver.Resolve("/goal ship it", Context());

        var refused = resolution.ShouldBeOfType<SlashRefused>();
        refused.Name.ShouldBe("goal");
        refused.Reason.ShouldContain("ACP", Case.Sensitive);
    }

    /// <summary>A catalogue token that Autonogrammer cannot run does not hide a discovered command of the same name: the user's own file wins over a refusal.</summary>
    [Fact]
    public void AnUnavailableTokenDoesNotHideADiscoveredCommand()
    {
        var discovery = new SlashDiscovery([Command("goal", "Work towards: $ARGUMENTS")], [], [], []);

        var resolution = Resolver.Resolve("/goal make it fast", Context(discovery: discovery));

        var send = resolution.ShouldBeOfType<SlashSendPrompt>();
        send.Prompt.ShouldBe("Work towards: make it fast");
        send.Chosen.Source.ShouldBe(SlashPaletteSource.Command);
        send.Chosen.Label.ShouldBe("Command, project");
    }

    /// <summary>A custom command with no placeholder gets the typed text after its body, so the arguments are not lost.</summary>
    [Fact]
    public void ADiscoveredCommandIsExpanded()
    {
        var discovery = new SlashDiscovery([Command("deploy", "Deploy $1 to $2.")], [], [], []);

        Resolver.Resolve("/deploy api staging", Context(discovery: discovery))
            .ShouldBeOfType<SlashSendPrompt>().Prompt.ShouldBe("Deploy api to staging.");
    }

    /// <summary>Precedence: catalogue, then engine, then command, then skill, then subagent. The first that can run the name is chosen and the others are alternatives with their labels.</summary>
    [Fact]
    public void WhenSeveralSourcesShareANameTheFirstInPrecedenceRunsAndTheOthersAreListed()
    {
        var discovery = new SlashDiscovery(
            [Command("ship", "Ship it.")],
            [Skill("ship", SlashScope.User)],
            [Subagent("ship")],
            []);
        var engine = new[] { new SlashEngineCommand("ship", "Engine ship", null) };

        var resolution = Resolver.Resolve("/ship now", Context(engine, discovery));

        var send = resolution.ShouldBeOfType<SlashSendPrompt>();
        send.Chosen.Source.ShouldBe(SlashPaletteSource.Engine);
        send.Prompt.ShouldBe("/ship now");
        send.Alternatives.Select(candidate => candidate.Label).ShouldBe(["Command, project", "Skill, user", "Subagent, project"]);
    }

    /// <summary>The engine stands for the catalogue row that delegates to it, and a discovered command of the same name is listed as an alternative.</summary>
    [Fact]
    public void TheEngineStandsForTheCatalogueRowAndADiscoveredCommandIsAnAlternative()
    {
        var discovery = new SlashDiscovery([Command("rename", "Rename it.")], [], [], []);
        var engine = new[] { new SlashEngineCommand("rename", "Rename the session", "[title]") };

        var resolution = Resolver.Resolve("/rename Plan", Context(engine, discovery));

        var send = resolution.ShouldBeOfType<SlashSendPrompt>();
        send.Chosen.Source.ShouldBe(SlashPaletteSource.Engine, "the catalogue row is the engine's own command, so the engine stands for it");
        send.Alternatives.Select(candidate => candidate.Source).ShouldBe([SlashPaletteSource.Command]);
    }

    /// <summary>A native action beats an engine command of the same name; the engine's row stays an alternative.</summary>
    [Fact]
    public void ANativeActionBeatsAnEngineCommandOfTheSameName()
    {
        var resolution = Resolver.Resolve(
            "/model opus",
            Context([new SlashEngineCommand("model", "Show current model selection", null)], host: new FixedHost(SlashActionIds.ModelPick)));

        var run = resolution.ShouldBeOfType<SlashRunAction>();
        run.Request.ActionId.ShouldBe(SlashActionIds.ModelPick);
        run.Request.TypedText.ShouldBe("opus");
        run.Alternatives.ShouldHaveSingleItem().Source.ShouldBe(SlashPaletteSource.Engine);
    }

    /// <summary>A discovered skill runs as a hand-off that names its file, or as omp's own skill command when omp advertises the skill.</summary>
    [Fact]
    public void ASkillIsHandedToOmp()
    {
        var discovery = new SlashDiscovery([], [Skill("create-rule")], [], []);

        var byFile = Resolver.Resolve("/create-rule no secrets in logs", Context(discovery: discovery));
        var byEngine = Resolver.Resolve(
            "/create-rule no secrets in logs",
            Context([new SlashEngineCommand("skill:create-rule", "Create rules", "arguments")], discovery));

        byFile.ShouldBeOfType<SlashSendPrompt>().Prompt.ShouldBe(
            "Use the skill \"create-rule\". Read its instructions from /work/.cursor/skills/create-rule/SKILL.md and follow them.\n\nno secrets in logs");
        byEngine.ShouldBeOfType<SlashSendPrompt>().Prompt.ShouldBe("/skill:create-rule no secrets in logs");
    }

    /// <summary>A skill that the engine advertises but discovery did not find (omp loaded it from its own providers) runs through omp's skill command.</summary>
    [Fact]
    public void ASkillOnlyTheEngineKnowsRunsThroughOmp()
    {
        var resolution = Resolver.Resolve("/review", Context([new SlashEngineCommand("skill:review", "Review", "arguments")]));

        var send = resolution.ShouldBeOfType<SlashSendPrompt>();
        send.Prompt.ShouldBe("/skill:review");
        send.Chosen.Source.ShouldBe(SlashPaletteSource.Engine);
    }

    /// <summary>A Cursor built-in skill with no skill file of that name is refused with the reason that says what to add.</summary>
    [Fact]
    public void ABuiltInSkillWithoutAFileIsRefused()
    {
        Resolver.Resolve("/create-rule x", Context())
            .ShouldBe(new SlashRefused("create-rule", SlashPaletteCopy.NoSkillInstalled("create-rule")));
    }

    /// <summary>A skill that is not user-invocable is not offered after a slash, so its name falls through to the engine as typed.</summary>
    [Fact]
    public void ASkillThatIsNotUserInvocableFallsThrough()
    {
        var discovery = new SlashDiscovery([], [Skill("internal-only", userInvocable: false)], [], []);

        Resolver.Resolve("/internal-only x", Context(discovery: discovery)).ShouldBe(new SlashPassThrough("/internal-only x"));
    }

    /// <summary>A subagent runs as a hand-off that carries its definition and the task.</summary>
    [Fact]
    public void ASubagentIsHandedTheTask()
    {
        var discovery = new SlashDiscovery([], [], [Subagent("verifier")], []);

        var send = Resolver.Resolve("/verifier check the login flow", Context(discovery: discovery)).ShouldBeOfType<SlashSendPrompt>();

        send.Chosen.Source.ShouldBe(SlashPaletteSource.Subagent);
        send.Prompt.ShouldContain("subagent named \"verifier\"", Case.Sensitive);
        send.Prompt.ShouldContain("Check the claims.", Case.Sensitive);
        send.Prompt.ShouldEndWith("Task: check the login flow", Case.Sensitive);
    }

    /// <summary>The cloud prefix is refused while no host can run the hand-off, and runs the hand-off action once a host can run it.</summary>
    [Fact]
    public void TheAmpersandPrefixIsTheCloudHandoff()
    {
        Resolver.Resolve("& refactor the auth module", Context())
            .ShouldBe(new SlashRefused("&", SlashPaletteCopy.ActionNotInThisWindow));

        var registry = new SlashCommandRegistry(
        [
            new SlashCommandDescriptor
            {
                Name = "in-cloud",
                Description = "Hands a task to a cloud agent.",
                Platforms = CommandPlatforms.Linux,
                HandlerId = SlashActionIds.CloudHandoff,
                Kind = SlashHandlerKind.CloudHandoff,
                Status = SlashCommandStatus.Built,
            },
        ]);
        var live = new SlashCommandResolver(registry).Resolve("& refactor the auth module", Context(host: new FixedHost(SlashActionIds.CloudHandoff)));

        live.ShouldBeOfType<SlashRunAction>().Request.ShouldBe(new SlashActionRequest(SlashActionIds.CloudHandoff, string.Empty, "refactor the auth module"));
    }

    /// <summary>The slash command in a name with a colon (omp's own skill form) is read as typed, not cut at the colon.</summary>
    [Fact]
    public void AColonNameResolvesAgainstTheEngineList()
    {
        var engine = new[] { new SlashEngineCommand("skill:review", "Review", "arguments") };

        var send = Resolver.Resolve("/skill:review the diff", Context(engine)).ShouldBeOfType<SlashSendPrompt>();

        send.Prompt.ShouldBe("/skill:review the diff");
        send.Chosen.Source.ShouldBe(SlashPaletteSource.Engine);
    }

    /// <summary>Null input is refused.</summary>
    [Fact]
    public void NullArgumentsAreRefused()
    {
        Should.Throw<ArgumentNullException>(() => Resolver.Resolve(null!, Context()));
        Should.Throw<ArgumentNullException>(() => Resolver.Resolve("/x", null!));
        Should.Throw<ArgumentNullException>(() => new SlashCommandResolver(null!));
    }

    private sealed class FixedHost(params string[] runnable) : ISlashActionHost
    {
        public bool CanRun(string actionId) => runnable.Contains(actionId, StringComparer.Ordinal);

        public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken) => Task.FromResult(SlashActionResult.Completed());
    }
}
