using OmpOS.Native.Composition.Mentions;
using OmpOS.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Composition.Mentions;

/// <summary>The rules and skills a mention offers, found on disk through the convention catalog.</summary>
public sealed class ConventionMentionProviderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static MentionWorkspaceTree WorkspaceWithConventions()
    {
        var tree = new MentionWorkspaceTree();
        tree.Write(".cursor/rules/style.mdc", "---\ndescription: Code style\nglobs: **/*.cs\nalwaysApply: false\n---\n\nUse tabs for indentation.\n");
        tree.Write(".cursor/rules/manual.mdc", "---\ndescription: Release checklist\nalwaysApply: false\n---\nRun the release checklist.\n");
        tree.Write(".cursor/rules/always.mdc", "---\ndescription: Always on\nalwaysApply: true\n---\nAlways be polite.\n");
        tree.Write("AGENTS.md", "Instructions for every agent.\n");
        tree.Write(".cursor/skills/deploy/SKILL.md", "---\ndescription: Deploy the app\n---\nRun the deploy script.\n");
        tree.Write(".cursor/skills/internal/SKILL.md", "---\ndescription: Not for the menu\nuser-invocable: false\n---\nInternal only.\n");
        return tree;
    }

    /// <summary>Rules that always apply (and instruction files) are left out; every other rule is offered with its description and the scope project. Skills that cannot be invoked by the user are left out.</summary>
    [Fact]
    public async Task OnlyRulesThatDoNotAlwaysApplyAndInvocableSkillsAreOffered()
    {
        using var tree = WorkspaceWithConventions();
        var provider = new ConventionMentionProvider([tree.Root], null);

        var rules = await provider.ListRulesAsync(Token).ConfigureAwait(true);
        var skills = await provider.ListSkillsAsync(Token).ConfigureAwait(true);

        rules.ShouldBe(
            [new MentionConventionItem("manual", "Release checklist", "project"), new MentionConventionItem("style", "Code style", "project")],
            ignoreOrder: true);
        skills.ShouldBe([new MentionConventionItem("deploy", "Deploy the app", "project")], ignoreOrder: true);
    }

    /// <summary>A body is read without its front matter, and is cut at the byte cap on a character boundary with the full size reported.</summary>
    [Fact]
    public async Task ABodyIsReadWithoutFrontMatterAndCutAtTheCap()
    {
        using var tree = WorkspaceWithConventions();
        tree.Write(".cursor/rules/wide.mdc", "---\ndescription: Wide characters\n---\n\u00E9\u00E9\u00E9\u00E9\n");
        var provider = new ConventionMentionProvider([tree.Root], null);

        var whole = await provider.ReadAsync(MentionKind.Rule, "style", 1000, Token).ConfigureAwait(true);
        var skill = await provider.ReadAsync(MentionKind.Skill, "deploy", 1000, Token).ConfigureAwait(true);
        var cut = await provider.ReadAsync(MentionKind.Rule, "wide", 5, Token).ConfigureAwait(true);

        whole.Text.ShouldBe("Use tabs for indentation.\n");
        whole.Truncated.ShouldBeFalse();
        skill.Text.ShouldBe("Run the deploy script.\n");
        cut.Text.ShouldBe("\u00E9\u00E9");
        cut.SizeBytes.ShouldBe(9);
        cut.Truncated.ShouldBeTrue();
    }

    /// <summary>A user skill is listed with the scope user and read from the user's folder, not the workspace.</summary>
    [Fact]
    public async Task AUserSkillIsReadFromTheUserHome()
    {
        using var tree = WorkspaceWithConventions();
        var home = Path.Combine(tree.Base, "home");
        Directory.CreateDirectory(Path.Combine(home, ".cursor", "skills", "personal"));
        await File.WriteAllTextAsync(Path.Combine(home, ".cursor", "skills", "personal", "SKILL.md"), "---\ndescription: My own\n---\nPersonal steps.\n", TestContext.Current.CancellationToken).ConfigureAwait(true);
        var provider = new ConventionMentionProvider([tree.Root], home);

        var skills = await provider.ListSkillsAsync(Token).ConfigureAwait(true);
        var body = await provider.ReadAsync(MentionKind.Skill, "personal", 1000, Token).ConfigureAwait(true);

        skills.ShouldContain(new MentionConventionItem("personal", "My own", "user"));
        body.Text.ShouldBe("Personal steps.\n");
    }

    /// <summary>The discovery is kept until <c>RefreshAsync</c>: a rule written meanwhile appears only after it.</summary>
    [Fact]
    public async Task TheDiscoveryIsKeptUntilItIsRefreshed()
    {
        using var tree = WorkspaceWithConventions();
        var provider = new ConventionMentionProvider([tree.Root], null);
        var before = await provider.ListRulesAsync(Token).ConfigureAwait(true);
        tree.Write(".cursor/rules/added.mdc", "---\ndescription: Added later\n---\nBody.\n");

        var cached = await provider.ListRulesAsync(Token).ConfigureAwait(true);
        await provider.RefreshAsync(Token).ConfigureAwait(true);
        var refreshed = await provider.ListRulesAsync(Token).ConfigureAwait(true);

        cached.Count.ShouldBe(before.Count);
        refreshed.Select(static item => item.Name).ShouldContain("added", StringComparer.Ordinal);
        refreshed.Count.ShouldBe(before.Count + 1);
    }

    /// <summary>A rule that is gone, an unknown name and a kind that is neither rule nor skill are refused with a plain sentence.</summary>
    [Fact]
    public async Task AnUnknownNameOrKindIsRefused()
    {
        using var tree = WorkspaceWithConventions();
        var provider = new ConventionMentionProvider([tree.Root], null);

        var unknown = await Should.ThrowAsync<MentionUnavailableException>(() => provider.ReadAsync(MentionKind.Rule, "nothing", 100, Token)).ConfigureAwait(true);
        var wrongKind = await Should.ThrowAsync<MentionUnavailableException>(() => provider.ReadAsync(MentionKind.File, "style", 100, Token)).ConfigureAwait(true);
        var gone = await Should.ThrowAsync<MentionUnavailableException>(() => provider.ReadAsync(MentionKind.Skill, "style", 100, Token)).ConfigureAwait(true);

        unknown.Message.ShouldBe("The rule 'nothing' no longer exists.");
        wrongKind.Message.ShouldBe("Only a rule or a skill can be read here.");
        gone.Message.ShouldBe("The skill 'style' no longer exists.");
    }

    /// <summary>A workspace with no convention folders offers nothing and does not throw.</summary>
    [Fact]
    public async Task AWorkspaceWithoutConventionsOffersNothing()
    {
        using var tree = new MentionWorkspaceTree();
        var provider = new ConventionMentionProvider([tree.Root], null);

        var rules = await provider.ListRulesAsync(Token).ConfigureAwait(true);
        var skills = await provider.ListSkillsAsync(Token).ConfigureAwait(true);

        rules.ShouldBeEmpty();
        skills.ShouldBeEmpty();
    }
}
