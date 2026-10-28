using OmpOS.Native.Settings;
using OmpOS.Native.Tests.Settings.Head;
using OmpOS.Native.Views.Conventions;
using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Settings.Customize;
using OmpOS.Workspace.BuiltinSkills;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Conventions;

/// <summary>
/// The skills that ship with OmpOS, in the desktop head over the real <c>assets/skills</c> folder as the build copies it next to the application: discovery finds every one, each slash token the catalogue
/// marks as built resolves to one of them, and the Settings Skills page lists them as built in.
/// </summary>
public sealed class BuiltinSkillsDesktopTests : IDisposable
{
    private readonly HeadTempDirectory _workspace = new();

    /// <inheritdoc />
    public void Dispose() => _workspace.Dispose();

    private static string[] ShippedNames() => [.. BuiltinSkillManifest.Shipped.Select(static skill => skill.Name)];

    private async Task<SlashDiscovery> DiscoverAsync() =>
        await new ConventionSlashDiscoverySource([_workspace.Root], null).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

    /// <summary>The skills folder is next to the test application and the default desktop discovery finds the ten shipped skills in the built-in scope, each as a file on disk, with no problem.</summary>
    [Fact]
    public async Task TheDesktopDiscoveryFindsEveryShippedSkillInTheRealFolder()
    {
        BuiltinSkillSource.CreateDefault().FolderExists.ShouldBeTrue($"{BuiltinSkillSource.CreateDefault().SkillsPath} is where the build copies assets/skills");

        var discovery = await DiscoverAsync().ConfigureAwait(true);

        discovery.Skills.Select(static skill => skill.Name).Order(StringComparer.Ordinal).ShouldBe(ShippedNames());
        discovery.Skills.ShouldAllBe(static skill => skill.Scope == SlashScope.BuiltIn && skill.UserInvocable && File.Exists(skill.Path));
        discovery.Problems.ShouldBeEmpty();
    }

    /// <summary>Every slash token the catalogue says a skill runs resolves to a shipped skill, and the shipped skills cover exactly the tokens the catalogue says are built from skills.</summary>
    [Fact]
    public async Task EveryBuiltSkillTokenOfTheCatalogueResolvesToAShippedSkill()
    {
        var discovery = await DiscoverAsync().ConfigureAwait(true);
        var tokens = SlashCommandCatalogue.Shipped.ListAll()
            .Where(static command => command.Kind == SlashHandlerKind.Skill && command.Status == SlashCommandStatus.Built)
            .Select(static command => command.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        tokens.ShouldBe(["agent-review", "create-hook", "create-rule", "create-skill", "create-subagent", "migrate-to-skills", "review", "review-security", "split-to-prs", "update-settings"]);
        tokens.ShouldBe(ShippedNames());
        var resolver = new SlashCommandResolver(SlashCommandCatalogue.Shipped);
        var context = new SlashResolutionContext([], discovery, null);
        foreach (var token in tokens)
        {
            var resolution = resolver.Resolve("/" + token + " the current change", context).ShouldBeOfType<SlashSendPrompt>($"/{token}");
            resolution.Chosen.Source.ShouldBe(SlashPaletteSource.Skill, $"/{token}");
            resolution.Chosen.Scope.ShouldBe(SlashScope.BuiltIn, $"/{token}");
            resolution.Chosen.Origin.ShouldBe($"builtin:skills/{token}/SKILL.md", $"/{token}");
            resolution.Prompt.ShouldContain(Path.Combine("skills", token, "SKILL.md"), Case.Sensitive, $"/{token} points omp at the shipped file");
            resolution.Prompt.ShouldEndWith("the current change", Case.Sensitive, $"/{token} keeps what the user typed");
        }
    }

    /// <summary>The slash resolution of /review is the skill with the Skill badge and no scope tag (built in); a project skill of the same name replaces it.</summary>
    [Fact]
    public async Task ReviewResolvesToTheBuiltinSkillUntilAProjectSkillTakesOver()
    {
        var resolver = new SlashCommandResolver(SlashCommandCatalogue.Shipped);

        var builtin = resolver.Resolve("/review", new SlashResolutionContext([], await DiscoverAsync().ConfigureAwait(true), null)).ShouldBeOfType<SlashSendPrompt>();

        builtin.Chosen.Label.ShouldBe("Skill", "a built-in skill shows the badge only");
        builtin.Chosen.Scope.ShouldBe(SlashScope.BuiltIn);
        builtin.Alternatives.ShouldBeEmpty("the catalogue row for /review is covered by the skill");

        _ = _workspace.Write(".cursor/skills/review/SKILL.md", "---\ndescription: Our review\n---\nOurs.\n");
        var ours = resolver.Resolve("/review", new SlashResolutionContext([], await DiscoverAsync().ConfigureAwait(true), null)).ShouldBeOfType<SlashSendPrompt>();

        ours.Chosen.Scope.ShouldBe(SlashScope.Project);
        ours.Chosen.Label.ShouldBe("Skill, project");
        ours.Prompt.ShouldContain(".cursor", Case.Sensitive);
    }

    /// <summary>The palette lists each shipped skill once, enabled, with the Skill badge and the built-in scope; the catalogue rows they replace are not listed twice; the Cursor-only skills stay disabled with their reason.</summary>
    [Fact]
    public async Task ThePaletteListsTheShippedSkillsAsEnabledSkillRows()
    {
        var palette = new SlashPaletteModel(SlashCommandCatalogue.Shipped);
        palette.SetDiscovered(await DiscoverAsync().ConfigureAwait(true));

        palette.UpdateDraft("/rev");

        var rows = palette.Entries.Where(static entry => entry.Name.StartsWith("review", StringComparison.Ordinal)).ToArray();
        rows.Select(static entry => entry.Name).ShouldBe(["review", "review-bugbot", "review-security"]);
        var review = rows[0];
        review.Source.ShouldBe(SlashPaletteSource.Skill);
        review.Scope.ShouldBe(SlashScope.BuiltIn);
        review.IsEnabled.ShouldBeTrue();
        rows[2].Source.ShouldBe(SlashPaletteSource.Skill);
        rows[2].IsEnabled.ShouldBeTrue();
        var bugbot = rows[1];
        bugbot.Source.ShouldBe(SlashPaletteSource.Cursor);
        bugbot.IsEnabled.ShouldBeFalse();
        bugbot.DisabledReason.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>The Skills page lists the shipped skills as built in, and a project skill of the same name replaces the entry without raising a problem.</summary>
    [Fact]
    public async Task TheSkillsPageListsTheShippedSkillsAsBuiltInAndAReplacementIsNotAProblem()
    {
        var page = new SkillsSettingsPage(new SkillsSettingsSource(_workspace.Root, null));
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Entries.Select(static entry => entry.Title).Order(StringComparer.Ordinal).ShouldBe(ShippedNames());
        page.Entries.ShouldAllBe(static entry => entry.Scope == CustomizeScope.BuiltIn);
        page.Problems.ShouldBeEmpty();

        _ = _workspace.Write(".agents/skills/review/SKILL.md", "---\ndescription: Our review\n---\nOurs.\n");
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Entries.Single(static entry => string.Equals(entry.Title, "review", StringComparison.Ordinal)).Scope.ShouldBe(CustomizeScope.Project);
        page.Entries.Count.ShouldBe(BuiltinSkillManifest.Shipped.Count, "the replaced built-in entry is not listed twice");
        page.Problems.ShouldBeEmpty("replacing a built-in skill is not a problem");
    }
}
