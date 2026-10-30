using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Customize;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Customize;

/// <summary>The read-only Rules, Skills and Commands pages: sections, states, entries and the absence of anything to edit or save.</summary>
public sealed class CustomizePagesTests
{
    private static readonly CustomizeEntry Rule = new("style", CustomizeScope.Project, ".autonogrammer/rules/style.md", "Use file-scoped namespaces.");

    private static (CustomizeCatalogPage Page, ScriptedCustomizeSource Source)[] AllPages()
    {
        var rules = new ScriptedCustomizeSource();
        var skills = new ScriptedCustomizeSource();
        var commands = new ScriptedCustomizeSource();
        return
        [
            (new RulesSettingsPage(rules), rules),
            (new SkillsSettingsPage(skills), skills),
            (new CommandsSettingsPage(commands), commands),
        ];
    }

    /// <summary>Each page fills its own section with its own title, intro and empty text.</summary>
    [Fact]
    public void PagesFillTheirSections()
    {
        var pages = AllPages().Select(entry => entry.Page).ToArray();

        pages.Select(page => page.Section).ShouldBe([SettingsSection.Rules, SettingsSection.Skills, SettingsSection.Commands]);
        pages.Select(page => page.Title).ShouldBe([CustomizeCopy.RulesTitle, CustomizeCopy.SkillsTitle, CustomizeCopy.CommandsTitle]);
        pages.Select(page => page.IntroText).Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
        pages.Select(page => page.EmptyText).Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
        pages.ShouldAllBe(page => page.State == CustomizePageState.Loading);
    }

    /// <summary>Loading, ready, empty and error are four different states.</summary>
    [Fact]
    public async Task StatesAreDistinct()
    {
        var token = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource();
        var loadingSource = new ScriptedCustomizeSource { Catalog = new CustomizeCatalog([Rule], []), Gate = gate };
        var page = new RulesSettingsPage(loadingSource);
        var pending = page.LoadAsync(token);
        var loading = page.State;
        gate.SetResult();
        await pending.ConfigureAwait(true);
        var ready = page.State;

        var emptyPage = new SkillsSettingsPage(new ScriptedCustomizeSource());
        await emptyPage.LoadAsync(token).ConfigureAwait(true);

        var errorPage = new CommandsSettingsPage(new ScriptedCustomizeSource { FailureMessage = "The registry is unavailable." });
        await errorPage.LoadAsync(token).ConfigureAwait(true);

        new[] { loading, ready, emptyPage.State, errorPage.State }.Distinct().Count().ShouldBe(4);
        ready.ShouldBe(CustomizePageState.Ready);
        page.Entries.ShouldBe([Rule]);
        page.CountText.ShouldBe("1 entry.");
        emptyPage.State.ShouldBe(CustomizePageState.Empty);
        emptyPage.Entries.ShouldBeEmpty();
        errorPage.State.ShouldBe(CustomizePageState.Error);
        errorPage.LoadFailure.ShouldBe("The registry is unavailable.");
    }

    /// <summary>A blank failure falls back to the copy and a later load recovers; problems of unreadable files are listed.</summary>
    [Fact]
    public async Task BlankFailureFallsBackAndReloadRecoversWithProblems()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedCustomizeSource { FailureMessage = " ", Catalog = new CustomizeCatalog([Rule], ["big.md is larger than 256 KiB."]) };
        var page = new RulesSettingsPage(source);

        await page.LoadAsync(token).ConfigureAwait(true);
        page.State.ShouldBe(CustomizePageState.Error);
        page.LoadFailure.ShouldBe(CustomizeCopy.LoadFailed);

        source.FailureMessage = null;
        await page.LoadAsync(token).ConfigureAwait(true);
        page.State.ShouldBe(CustomizePageState.Ready);
        page.LoadFailure.ShouldBeNull();
        page.Problems.ShouldBe(["big.md is larger than 256 KiB."]);
    }

    /// <summary>Cancelling a load throws and leaves the page as it was.</summary>
    [Fact]
    public async Task CancellingALoadRestoresTheState()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedCustomizeSource { Catalog = new CustomizeCatalog([Rule], []) };
        var page = new RulesSettingsPage(source);
        await page.LoadAsync(token).ConfigureAwait(true);
        source.Gate = new TaskCompletionSource();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);

        var pending = page.LoadAsync(cancellation.Token);
        page.State.ShouldBe(CustomizePageState.Loading);
        await cancellation.CancelAsync().ConfigureAwait(true);

        Exception? thrown = null;
        try
        {
            await pending.ConfigureAwait(true);
        }
        catch (OperationCanceledException exception)
        {
            thrown = exception;
        }

        thrown.ShouldBeAssignableTo<OperationCanceledException>();
        page.State.ShouldBe(CustomizePageState.Ready);
        page.Entries.Count.ShouldBe(1);
    }

    /// <summary>The pages have nothing to save: never dirty, never in error, saving succeeds without reading, reset and discard change nothing.</summary>
    [Fact]
    public async Task PagesAreReadOnly()
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var (page, source) in AllPages())
        {
            source.Catalog = new CustomizeCatalog([Rule], []);
            await page.LoadAsync(token).ConfigureAwait(true);
            var loads = source.LoadCalls;

            page.IsDirty.ShouldBeFalse();
            page.HasError.ShouldBeFalse();
            (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();
            page.Reset();
            page.Discard();

            source.LoadCalls.ShouldBe(loads);
            page.Entries.ShouldBe([Rule]);
            page.IsDirty.ShouldBeFalse();
        }
    }

    /// <summary>An entry shows its title, scope words, origin and description; the scope words differ per scope.</summary>
    [Fact]
    public void EntriesCarryScopeWords()
    {
        Rule.ScopeLabel.ShouldBe("Project");
        new CustomizeEntry("t", CustomizeScope.User, "~/x.md", string.Empty).ScopeLabel.ShouldBe("User");
        new CustomizeEntry("t", CustomizeScope.BuiltIn, "x", string.Empty).ScopeLabel.ShouldBe("Built in");
    }

    /// <summary>Every public constant of the copy is worded, every scope has words, and the count text pluralises.</summary>
    [Fact]
    public void CopyIsComplete()
    {
        var constants = typeof(CustomizeCopy).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .ToArray();

        constants.Length.ShouldBeGreaterThan(15);
        foreach (var constant in constants)
        {
            ((string)constant.GetRawConstantValue()!).ShouldNotBeNullOrWhiteSpace(constant.Name);
        }

        Enum.GetValues<CustomizeScope>().Select(CustomizeCopy.ScopeLabel).Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
        Should.Throw<ArgumentOutOfRangeException>(() => CustomizeCopy.ScopeLabel((CustomizeScope)99));
        CustomizeCopy.CountText(0).ShouldBe("0 entries.");
        CustomizeCopy.CountText(1).ShouldBe("1 entry.");
        CustomizeCopy.CountText(12).ShouldBe("12 entries.");
    }
}
