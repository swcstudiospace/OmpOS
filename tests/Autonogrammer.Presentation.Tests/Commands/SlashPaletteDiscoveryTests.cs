using Autonogrammer.Presentation.Commands;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Commands;

/// <summary>The palette lists the commands, skills and subagents that discovery found, with their badges and scopes, and disables the native rows the window cannot run (PX-13).</summary>
public sealed class SlashPaletteDiscoveryTests
{
    private static SlashPaletteModel Shipped() => new(SlashCommandCatalogue.Shipped);

    private static SlashDiscovery Discovery() => new(
        [new SlashDiscoveredCommand("deploy", "Deploys.", "<env>", "Deploy $1", SlashScope.Project, ".cursor/commands/deploy.md")],
        [
            new SlashDiscoveredSkill("create-rule", "Creates rules.", true, "/w/.cursor/skills/create-rule/SKILL.md", SlashScope.User, "~/.cursor/skills/create-rule/SKILL.md"),
            new SlashDiscoveredSkill("hidden", "Hidden.", false, "/w/.cursor/skills/hidden/SKILL.md", SlashScope.Project, ".cursor/skills/hidden/SKILL.md"),
        ],
        [new SlashDiscoveredSubagent("verifier", "Verifies.", null, ReadOnly: false, IsBackground: false, "Check.", SlashScope.Project, ".cursor/agents/verifier.md")],
        []);

    /// <summary>Discovered rows carry their badge and scope, a skill that is not user-invocable is not listed, and the row's accessible name says the source and the scope.</summary>
    [Fact]
    public void DiscoveredRowsCarryTheirBadgeAndScope()
    {
        var palette = Shipped();
        palette.SetDiscovered(Discovery());

        palette.UpdateDraft("/");

        var deploy = palette.Entries.Single(entry => string.Equals(entry.Name, "deploy", StringComparison.Ordinal));
        deploy.Source.ShouldBe(SlashPaletteSource.Command);
        deploy.SourceLabel.ShouldBe("Command");
        deploy.ScopeLabel.ShouldBe("project");
        deploy.InputHint.ShouldBe("<env>");
        deploy.AutomationName.ShouldBe("/deploy, <env>, Deploys., Command, project");
        var verifier = palette.Entries.Single(entry => string.Equals(entry.Name, "verifier", StringComparison.Ordinal));
        verifier.Source.ShouldBe(SlashPaletteSource.Subagent);
        verifier.InputHint.ShouldBe(SlashPaletteCopy.SubagentHint);
        palette.Entries.ShouldNotContain(entry => string.Equals(entry.Name, "hidden", StringComparison.Ordinal));
    }

    /// <summary>A discovered skill of the same name stands for the Cursor built-in skill row, which is then not listed; without one, the row is listed disabled and says what to add.</summary>
    [Fact]
    public void ASkillFileStandsForTheBuiltInSkillRow()
    {
        var palette = Shipped();
        palette.UpdateDraft("/create-");

        palette.Entries.Select(entry => entry.Name).ShouldBe(["create-hook", "create-rule", "create-skill", "create-subagent"]);
        palette.Entries.ShouldAllBe(entry => !entry.IsEnabled && entry.Source == SlashPaletteSource.Cursor);

        palette.SetDiscovered(Discovery());

        var rule = palette.Entries.Single(entry => string.Equals(entry.Name, "create-rule", StringComparison.Ordinal));
        rule.Source.ShouldBe(SlashPaletteSource.Skill);
        rule.IsEnabled.ShouldBeTrue();
        rule.ScopeLabel.ShouldBe("user");
        palette.Entries.Count.ShouldBe(4, "the built-in row is replaced, not repeated");
    }

    /// <summary>A skill that omp advertises as skill:&lt;name&gt; also stands for the built-in row.</summary>
    [Fact]
    public void AnEngineSkillStandsForTheBuiltInSkillRow()
    {
        var palette = Shipped();
        palette.SetEngineCommands([new SlashEngineCommand("skill:review", "Review", "arguments")]);

        palette.UpdateDraft("/rev");

        palette.Entries.Select(entry => entry.Name).ShouldBe(["review-bugbot", "review-security"], "the /review row is dropped for omp's skill row");
    }

    /// <summary>When two sources share a name, both rows are listed, each with its badge, the catalogue first.</summary>
    [Fact]
    public void ASharedNameIsListedOncePerSource()
    {
        var palette = Shipped();
        palette.SetDiscovered(new SlashDiscovery([new SlashDiscoveredCommand("goal", "My goal.", null, "Goal: $ARGUMENTS", SlashScope.User, "~/.claude/commands/goal.md")], [], [], []));
        palette.SetEngineCommands([new SlashEngineCommand("goal", "Engine goal", null)]);

        palette.UpdateDraft("/goal");

        palette.Entries.Select(entry => entry.SourceLabel).ShouldBe(["Cursor", "Engine", "Command"]);
        palette.Entries[0].IsEnabled.ShouldBeFalse();
        palette.Entries[1].IsEnabled.ShouldBeTrue();
        palette.Entries[2].IsEnabled.ShouldBeTrue();
    }

    /// <summary>A native row is disabled while no host can run its action, and enabled when the host can; a host change updates an open palette.</summary>
    [Fact]
    public void NativeRowsFollowTheHost()
    {
        var palette = Shipped();
        palette.UpdateDraft("/clear");
        var before = palette.Entries.ShouldHaveSingleItem();
        before.IsEnabled.ShouldBeFalse();
        before.DisabledReason.ShouldBe(SlashPaletteCopy.ActionNotInThisWindow);
        before.Source.ShouldBe(SlashPaletteSource.Autonogrammer);

        palette.SetActionHost(new OnlyHost(SlashActionIds.ChatNew));

        palette.Entries.ShouldHaveSingleItem().IsEnabled.ShouldBeTrue();
        palette.UpdateDraft("/config");
        palette.Entries.ShouldHaveSingleItem().IsEnabled.ShouldBeFalse("the host cannot open Settings");
    }

    /// <summary>Choosing a disabled row shows its reason, so a row that cannot run never looks like a control that does nothing.</summary>
    [Fact]
    public void ChoosingAnUnavailableRowShowsItsReason()
    {
        var palette = Shipped();
        palette.UpdateDraft("/goal");

        palette.Accept().ShouldBeNull();

        palette.Notice.ShouldNotBeNullOrWhiteSpace();
        palette.Notice.ShouldContain("ACP", Case.Sensitive);
    }

    /// <summary>Discovery and host setters refuse null where a value is required.</summary>
    [Fact]
    public void NullDiscoveryIsRefused()
    {
        Should.Throw<ArgumentNullException>(() => Shipped().SetDiscovered(null!));
    }

    private sealed class OnlyHost(string actionId) : ISlashActionHost
    {
        public bool CanRun(string requested) => string.Equals(requested, actionId, StringComparison.Ordinal);

        public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken) => Task.FromResult(SlashActionResult.Completed());
    }
}
