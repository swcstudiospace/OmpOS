using OmpOS.Workspace.BuiltinSkills;
using OmpOS.Workspace.Conventions;
using OmpOS.Workspace.Tests.Conventions;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests.BuiltinSkills;

/// <summary>
/// The skills bundled with the application (<c>assets/skills</c>, copied next to the assembly by the project file) agree with <see cref="BuiltinSkillManifest"/>, are well formed, and every slash token that the
/// command catalogue marks as served by a built-in skill resolves to one of them through <see cref="ConventionCatalog"/>.
/// </summary>
public sealed class BuiltinSkillManifestTests
{
    /// <summary>The slash tokens whose catalogue rows are Built with a skill handler. The command catalogue file is not read: this list is what the wiring pass sets.</summary>
    public static TheoryData<string> FlippedTokens { get; } = new()
    {
        "agent-review",
        "create-hook",
        "create-rule",
        "create-skill",
        "create-subagent",
        "migrate-to-skills",
        "review",
        "review-security",
        "split-to-prs",
        "update-settings",
    };

    private static async Task<ConventionSet> DiscoverBundleAsync() =>
        await ConventionCatalog.DiscoverAsync(
            ["/ws"],
            null,
            new ConventionSources(new InMemoryConventionFileSource(), new InMemoryConventionFileSource(), BuiltinSkillSource.CreateDefault()),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

    /// <summary>The bundle holds exactly the manifest's skills, and every one of them loads without a diagnostic (its folder name matches its <c>name</c>, its frontmatter closes).</summary>
    [Fact]
    public async Task TheBundleHoldsExactlyTheShippedSkills()
    {
        BuiltinSkillSource.CreateDefault().FolderExists.ShouldBeTrue("the skills folder must be copied next to the assembly");

        var set = await DiscoverBundleAsync().ConfigureAwait(true);

        set.Diagnostics.ShouldBeEmpty();
        set.Skills.Select(skill => skill.Name).Order(StringComparer.Ordinal).ShouldBe(BuiltinSkillManifest.Shipped.Select(entry => entry.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>Every bundled skill is offered after a slash and to the model, has a real description that is not cut, and belongs to the built-in scope.</summary>
    [Fact]
    public async Task EveryBundledSkillIsInvocableAndDescribed()
    {
        var set = await DiscoverBundleAsync().ConfigureAwait(true);

        foreach (var skill in set.Skills)
        {
            skill.Scope.ShouldBe(ConventionScope.Builtin);
            skill.UserInvocable.ShouldBeTrue(skill.Name);
            skill.DisableModelInvocation.ShouldBeFalse(skill.Name);
            skill.Description.Length.ShouldBeGreaterThan(40, skill.Name);
            skill.Description.EndsWith("...", StringComparison.Ordinal).ShouldBeFalse(skill.Name + " has a description longer than the palette shows");
        }
    }

    /// <summary>A token that a flipped catalogue row serves is served by a shipped skill, and that skill is discovered under the same name, so typing the token runs it.</summary>
    /// <param name="token">The slash token.</param>
    [Theory]
    [MemberData(nameof(FlippedTokens))]
    public async Task EveryFlippedTokenResolvesToAShippedSkill(string token)
    {
        var entry = BuiltinSkillManifest.FindByToken(token);
        var set = await DiscoverBundleAsync().ConfigureAwait(true);

        var found = entry.ShouldNotBeNull();
        found.Name.ShouldBe(token, Case.Sensitive);
        set.Skills.Count(skill => string.Equals(skill.Name, token, StringComparison.Ordinal)).ShouldBe(1);
    }

    /// <summary>Every token of every shipped skill is the skill's own name first, and no token is served twice.</summary>
    [Fact]
    public void ManifestTokensAreUnique()
    {
        var tokens = BuiltinSkillManifest.Shipped.SelectMany(entry => entry.SlashTokens).ToList();

        tokens.Distinct(StringComparer.Ordinal).Count().ShouldBe(tokens.Count);
        BuiltinSkillManifest.Shipped.ShouldAllBe(entry => entry.SlashTokens.Count > 0 && string.Equals(entry.SlashTokens[0], entry.Name, StringComparison.Ordinal));
        BuiltinSkillManifest.Shipped.Select(entry => entry.Name).ShouldBe(BuiltinSkillManifest.Shipped.Select(entry => entry.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>A skill that is not applicable is not shipped, has a reason, and is listed once.</summary>
    [Fact]
    public async Task NotApplicableTokensAreNotShipped()
    {
        var set = await DiscoverBundleAsync().ConfigureAwait(true);

        BuiltinSkillManifest.NotApplicable.Select(item => item.Token).Distinct(StringComparer.Ordinal).Count().ShouldBe(BuiltinSkillManifest.NotApplicable.Count);
        foreach (var item in BuiltinSkillManifest.NotApplicable)
        {
            item.Reason.Length.ShouldBeGreaterThan(20, item.Token);
            BuiltinSkillManifest.FindByToken(item.Token).ShouldBeNull(item.Token);
            set.Skills.ShouldNotContain(skill => string.Equals(skill.Name, item.Token, StringComparison.Ordinal), item.Token);
        }
    }

    /// <summary>An unknown token is not served.</summary>
    [Fact]
    public void AnUnknownTokenIsNotServed()
    {
        BuiltinSkillManifest.FindByToken("no-such-skill").ShouldBeNull();
    }
}
