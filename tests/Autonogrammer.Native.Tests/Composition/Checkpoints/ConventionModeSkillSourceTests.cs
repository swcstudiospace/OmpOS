using Autonogrammer.Native.Composition.Checkpoints;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Modes;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition.Checkpoints;

/// <summary>The skill source of custom modes over a temporary <c>.cursor/skills</c> folder.</summary>
public sealed class ConventionModeSkillSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "autonogrammer-skills-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the temporary directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteSkill(string name, string text)
    {
        var directory = Path.Combine(_root, ".cursor", "skills", name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "SKILL.md"), text);
    }

    /// <summary>A user-invocable skill is listed with its description and project scope; one that is not invocable is left out.</summary>
    [Fact]
    public async Task OnlyUserInvocableSkillsAreListed()
    {
        var ct = TestContext.Current.CancellationToken;
        WriteSkill("review", "---\ndescription: Review the diff\nicon: eye\n---\nLook at every hunk.\n");
        WriteSkill("hidden", "---\ndescription: Model only\nuser-invocable: false\n---\nInternal.\n");
        var source = new ConventionModeSkillSource([_root], null);

        var skills = await source.ListAsync(ct).ConfigureAwait(true);

        var skill = skills.ShouldHaveSingleItem();
        skill.Name.ShouldBe("review");
        skill.Description.ShouldBe("Review the diff");
        skill.Icon.ShouldBe("eye");
        skill.Scope.ShouldBe(SlashScope.Project);
    }

    /// <summary>The body is the file text without its frontmatter.</summary>
    [Fact]
    public async Task TheBodyHasNoFrontmatter()
    {
        var ct = TestContext.Current.CancellationToken;
        WriteSkill("review", "---\ndescription: Review the diff\n---\nLook at every hunk.\n");
        var source = new ConventionModeSkillSource([_root], null);
        var skill = (await source.ListAsync(ct).ConfigureAwait(true)).Single();

        var body = await source.ReadBodyAsync(skill, ct).ConfigureAwait(true);

        body.ShouldNotBeNull().ShouldContain("Look at every hunk.", Case.Sensitive);
        body.ShouldNotContain("description:", Case.Sensitive);
    }

    /// <summary>A skill that was never listed has no known path, and the failure says so.</summary>
    [Fact]
    public async Task AnUnlistedSkillIsAClearError()
    {
        var source = new ConventionModeSkillSource([_root], null);
        var unknown = new ModeSkill("ghost", "none", null, null, SlashScope.Project, ".cursor/skills/ghost/SKILL.md");

        var failure = await Should.ThrowAsync<InvalidOperationException>(() => source.ReadBodyAsync(unknown, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.Message.ShouldContain("ghost", Case.Sensitive);
    }
}
