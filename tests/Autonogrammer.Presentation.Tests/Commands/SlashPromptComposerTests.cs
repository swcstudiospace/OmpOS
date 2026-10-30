using Autonogrammer.Presentation.Commands;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Commands;

/// <summary>The prompts that hand a discovered skill or subagent to the engine.</summary>
public sealed class SlashPromptComposerTests
{
    private static readonly SlashDiscoveredSkill Skill = new("lint", "Lints.", true, "/w/.claude/skills/lint/SKILL.md", SlashScope.Project, ".claude/skills/lint/SKILL.md");

    /// <summary>A skill that omp advertises runs as omp's own skill command; otherwise omp is told to read the skill file.</summary>
    [Theory]
    [InlineData(true, "", "/skill:lint")]
    [InlineData(true, "  src  ", "/skill:lint src")]
    [InlineData(false, "", "Use the skill \"lint\". Read its instructions from /w/.claude/skills/lint/SKILL.md and follow them.")]
    [InlineData(false, "src", "Use the skill \"lint\". Read its instructions from /w/.claude/skills/lint/SKILL.md and follow them.\n\nsrc")]
    public void ASkillIsHandedOverByCommandOrByFile(bool engineRunsSkill, string typed, string expected) =>
        SlashPromptComposer.ForSkill(Skill, typed, engineRunsSkill).ShouldBe(expected);

    /// <summary>A subagent prompt carries what the definition says: description, a model other than inherit, the read-only and background flags, the instructions and the task.</summary>
    [Fact]
    public void ASubagentPromptCarriesItsDefinition()
    {
        var agent = new SlashDiscoveredSubagent("verifier", "Verifies work.", "fast-model", ReadOnly: true, IsBackground: true, "  Check the claims.\n", SlashScope.User, "~/.cursor/agents/verifier.md");

        var prompt = SlashPromptComposer.ForSubagent(agent, " the login ");

        prompt.ShouldBe(
            "Hand the task below to a subagent named \"verifier\" with the task tool. Its definition comes from ~/.cursor/agents/verifier.md:\n"
            + "Description: Verifies work.\nModel: fast-model\nThe subagent must not change files.\nRun the subagent in the background.\n"
            + "Instructions:\nCheck the claims.\n\nTask: the login");
    }

    /// <summary>An inherited model, no description and no flags leave those lines out.</summary>
    [Fact]
    public void ASubagentPromptOmitsWhatIsAbsent()
    {
        var agent = new SlashDiscoveredSubagent("plain", string.Empty, "Inherit", ReadOnly: false, IsBackground: false, "Do it.", SlashScope.Project, ".cursor/agents/plain.md");

        SlashPromptComposer.ForSubagent(agent, "x").ShouldBe(
            "Hand the task below to a subagent named \"plain\" with the task tool. Its definition comes from .cursor/agents/plain.md:\nInstructions:\nDo it.\n\nTask: x");
    }

    /// <summary>Null input is refused.</summary>
    [Fact]
    public void NullArgumentsAreRefused()
    {
        Should.Throw<ArgumentNullException>(() => SlashPromptComposer.ForSkill(null!, "x", false));
        Should.Throw<ArgumentNullException>(() => SlashPromptComposer.ForSkill(Skill, null!, false));
        Should.Throw<ArgumentNullException>(() => SlashPromptComposer.ForSubagent(null!, "x"));
    }
}
