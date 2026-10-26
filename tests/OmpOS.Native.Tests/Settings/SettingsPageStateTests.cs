using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Native.Views.Settings;
using OmpOS.Presentation;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Customize;
using OmpOS.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings;

/// <summary>The loading, ready, failed-load and empty states of each settings page view, and the Try again button.</summary>
public sealed class SettingsPageStateTests
{
    private const string Failure = "omp could not be started.";

    private static string LoadingText(SettingsSection section) => section switch
    {
        SettingsSection.Models => ModelsCopy.LoadingText,
        SettingsSection.Rules or SettingsSection.Skills or SettingsSection.Commands => CustomizeCopy.LoadingText,
        _ => SettingsCopy.LoadingText,
    };

    private static string ErrorHeading(SettingsSection section) => section switch
    {
        SettingsSection.Models => ModelsCopy.ErrorTitle,
        SettingsSection.Rules or SettingsSection.Skills or SettingsSection.Commands => CustomizeCopy.ErrorTitle,
        _ => SettingsCopy.LoadFailedHeading,
    };

    private static string Heading(SettingsSection section) => section switch
    {
        SettingsSection.Runtime => SettingsCopy.RuntimeHeading,
        SettingsSection.Models => ModelsCopy.PageTitle,
        SettingsSection.General => SettingsCopy.GeneralHeading,
        SettingsSection.Agents => SettingsCopy.AgentsHeading,
        SettingsSection.Tab => SettingsCopy.TabHeading,
        SettingsSection.Plugins => SettingsCopy.PluginsHeading,
        SettingsSection.Rules => CustomizeCopy.RulesTitle,
        SettingsSection.Skills => CustomizeCopy.SkillsTitle,
        _ => CustomizeCopy.CommandsTitle,
    };

    private static void ScriptEveryLoad(SettingsFixture fixture, string? failure)
    {
        fixture.GeneralSource.LoadFailure = failure;
        fixture.AgentsSource.LoadFailure = failure;
        fixture.TabSource.LoadFailure = failure;
        fixture.PluginsSource.LoadFailure = failure;
        fixture.RulesSource.LoadFailure = failure;
        fixture.SkillsSource.LoadFailure = failure;
        fixture.CommandsSource.LoadFailure = failure;
        fixture.ModelsSource.LoadFailure = failure;
    }

    /// <summary>While a page loads its view says so in a live region and shows none of its controls; when the load ends the controls appear.</summary>
    [Theory]
    [InlineData(SettingsSection.Models)]
    [InlineData(SettingsSection.General)]
    [InlineData(SettingsSection.Agents)]
    [InlineData(SettingsSection.Tab)]
    [InlineData(SettingsSection.Plugins)]
    [InlineData(SettingsSection.Rules)]
    [InlineData(SettingsSection.Skills)]
    [InlineData(SettingsSection.Commands)]
    public async Task APageShowsLoadingUntilItsValuesArrive(SettingsSection section)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, load: false).ConfigureAwait(true);
            var view = (SettingsPageViewBase)fixture.Show(section);

            view.Status.IsLoadingShown.ShouldBeTrue();
            view.Status.IsErrorShown.ShouldBeFalse();
            view.Status.LoadingMessage.ShouldBe(LoadingText(section));
            SettingsTree.Texts(view).ShouldBe([LoadingText(section)]);

            await fixture.Model.LoadAllAsync(ct).ConfigureAwait(true);
            fixture.Settle();

            view.Status.IsLoadingShown.ShouldBeFalse();
            SettingsTree.Texts(view).ShouldContain(Heading(section), StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A page that cannot load shows why under its heading with a Try again button; the button loads it again and the controls appear.</summary>
    [Theory]
    [InlineData(SettingsSection.Models)]
    [InlineData(SettingsSection.General)]
    [InlineData(SettingsSection.Agents)]
    [InlineData(SettingsSection.Tab)]
    [InlineData(SettingsSection.Plugins)]
    [InlineData(SettingsSection.Rules)]
    [InlineData(SettingsSection.Skills)]
    [InlineData(SettingsSection.Commands)]
    public async Task AFailedLoadShowsItsReasonAndTryAgainLoadsItAgain(SettingsSection section)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, f => ScriptEveryLoad(f, Failure)).ConfigureAwait(true);
            var view = (SettingsPageViewBase)fixture.Show(section);

            view.Status.IsErrorShown.ShouldBeTrue();
            view.Status.IsLoadingShown.ShouldBeFalse();
            view.Status.ErrorMessage.ShouldBe(Failure);
            SettingsTree.Texts(view).ShouldContain(ErrorHeading(section), StringComparer.Ordinal);
            SettingsTree.Texts(view).ShouldContain(Failure, StringComparer.Ordinal);
            SettingsTree.Named<Button>(view, SettingsCopy.RetryLabel).ShouldBeSameAs(view.Status.Retry);

            ScriptEveryLoad(fixture, null);
            SettingsTree.Click(fixture.Window, view.Status.Retry);

            view.Status.IsErrorShown.ShouldBeFalse();
            SettingsTree.Texts(view).ShouldContain(Heading(section), StringComparer.Ordinal);
            SettingsTree.Texts(view).ShouldNotContain(Failure, StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>An empty plugin list says so; a catalogue that lists nothing keeps its heading and explains why.</summary>
    [Fact]
    public async Task EmptyStatesSayWhatIsMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(
                ct,
                static f =>
                {
                    f.PluginsSource.Values = new PluginsSettingsValues([]);
                    f.RulesSource.Values = new CustomizeCatalog([], []);
                }).ConfigureAwait(true);

            var plugins = (PluginsPageView)fixture.Show(SettingsSection.Plugins);
            plugins.Empty.IsVisible.ShouldBeTrue();
            SettingsTree.Texts(plugins).ShouldContain(SettingsCopy.PluginsEmpty, StringComparer.Ordinal);

            var rules = (CustomizeCatalogView)fixture.Show(SettingsSection.Rules);
            var texts = SettingsTree.Texts(rules);
            rules.EmptyText.IsVisible.ShouldBeTrue();
            texts.ShouldContain(CustomizeCopy.RulesTitle, StringComparer.Ordinal);
            texts.ShouldContain(fixture.Rules.EmptyText, StringComparer.Ordinal);
            texts.ShouldNotContain(CustomizeCopy.ColumnTitle, StringComparer.Ordinal, "no table is drawn for an empty catalogue");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The Plugins page lists every plugin with its role, availability and version, and says when a version is not known.</summary>
    [Fact]
    public async Task ThePluginsPageListsEveryPluginWithItsAvailability()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (PluginsPageView)fixture.Show(SettingsSection.Plugins);
            var texts = SettingsTree.Texts(view);

            view.Empty.IsVisible.ShouldBeFalse();
            texts.ShouldContain(SettingsCopy.PluginsAlwaysOnNote, StringComparer.Ordinal);
            foreach (var row in fixture.Plugins.Rows)
            {
                texts.ShouldContain(row.DisplayName, StringComparer.Ordinal);
                texts.ShouldContain(row.Role, StringComparer.Ordinal);
            }

            texts.Count(static text => string.Equals(text, SettingsCopy.PluginFoundLabel, StringComparison.Ordinal)).ShouldBe(3);
            texts.Count(static text => string.Equals(text, SettingsCopy.PluginMissingLabel, StringComparison.Ordinal)).ShouldBe(1);
            texts.ShouldContain(SettingsCopy.PluginVersionUnknown, StringComparer.Ordinal);
            texts.ShouldContain("1.2.0", StringComparer.Ordinal);
            texts.ShouldContain("Not installed", StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The Tab page shows the completion status, the endpoint host and how it is configured, and the off state says none is configured.</summary>
    [Fact]
    public async Task TheTabPageShowsTheStatusOfInlineCompletion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (TabPageView)fixture.Show(SettingsSection.Tab);

            view.StatusText.Text.ShouldBe(SettingsCopy.TabStatusOn);
            view.Endpoint.Text.ShouldBe(fixture.Tab.EndpointText);
            view.Endpoint.Text.ShouldNotBeNull().ShouldContain("localhost");
            view.HowConfigured.Text.ShouldBe(SettingsCopy.TabHowConfigured(enabled: true, "OMPOS_COMPLETION_URL"));

            fixture.TabSource.Values = new TabSettingsValues(Enabled: false, EndpointHost: null, Source: string.Empty);
            await fixture.Tab.LoadAsync(ct).ConfigureAwait(true);
            fixture.Settle();

            view.StatusText.Text.ShouldBe(SettingsCopy.TabStatusOff);
            view.Endpoint.Text.ShouldBe(SettingsCopy.TabEndpointNone);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The General page offers the three motion choices with their words, selects the draft's choice, and a choice becomes the draft.</summary>
    [Fact]
    public async Task TheGeneralPageOffersTheMotionChoices()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (GeneralPageView)fixture.Show(SettingsSection.General);
            var items = view.Motion.Items.OfType<ListBoxItem>().ToList();

            items.Count.ShouldBe(3);
            var texts = SettingsTree.Texts(view);
            foreach (var option in GeneralPage.MotionOptions)
            {
                texts.ShouldContain(option.Label, StringComparer.Ordinal);
                texts.ShouldContain(option.Description, StringComparer.Ordinal);
            }

            texts.ShouldContain(SettingsCopy.PrivacyStatement, StringComparer.Ordinal);
            view.Motion.SelectedItem.ShouldBeSameAs(items[0]);

            view.Motion.SelectedItem = items[2];
            fixture.Settle();

            fixture.General.Motion.ShouldBe(MotionPreference.Full);
            fixture.General.IsDirty.ShouldBeTrue();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The General page offers the three theme choices in the order System, Light, Dark with their words, selects the saved Dark by default, and a choice becomes the draft.</summary>
    [Fact]
    public async Task TheGeneralPageOffersTheThemeChoices()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (GeneralPageView)fixture.Show(SettingsSection.General);
            var items = view.ThemeChoices.Items.OfType<ListBoxItem>().ToList();

            items.Select(static item => ((ThemeOption)item.Tag!).Value).ToArray()
                .ShouldBe([ThemePreference.System, ThemePreference.Light, ThemePreference.Dark]);
            var texts = SettingsTree.Texts(view);
            texts.ShouldContain(SettingsCopy.ThemeLabel, StringComparer.Ordinal);
            foreach (var option in GeneralPage.ThemeOptions)
            {
                texts.ShouldContain(option.Label, StringComparer.Ordinal);
                texts.ShouldContain(option.Description, StringComparer.Ordinal);
            }

            view.ThemeChoices.SelectedItem.ShouldBeSameAs(items[2]);
            fixture.General.IsDirty.ShouldBeFalse();

            view.ThemeChoices.SelectedItem = items[1];
            fixture.Settle();

            fixture.General.Theme.ShouldBe(ThemePreference.Light);
            fixture.General.IsDirty.ShouldBeTrue();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The Rules page lists each entry with its scope and origin, says how many there are in a live region, and names a file that could not be read.</summary>
    [Fact]
    public async Task ACatalogListsItsEntriesAndTheFilesItCouldNotRead()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(
                ct,
                static f => f.RulesSource.Values = f.RulesSource.Values with { Problems = ["big.md is larger than the limit."] }).ConfigureAwait(true);
            var view = (CustomizeCatalogView)fixture.Show(SettingsSection.Rules);
            var texts = SettingsTree.Texts(view);

            texts.ShouldContain("Be brief", StringComparer.Ordinal);
            texts.ShouldContain(CustomizeCopy.ScopeLabel(CustomizeScope.Project), StringComparer.Ordinal);
            texts.ShouldContain(".ompos/rules/brief.md", StringComparer.Ordinal);
            texts.ShouldContain("Answer in few words.", StringComparer.Ordinal);
            texts.ShouldContain(CustomizeCopy.ReadOnlyNotice, StringComparer.Ordinal);
            texts.ShouldContain(CustomizeCopy.ColumnTitle, StringComparer.Ordinal);
            texts.ShouldContain(CustomizeCopy.ProblemsHeading, StringComparer.Ordinal);
            texts.ShouldContain("big.md is larger than the limit.", StringComparer.Ordinal);
            view.Count.Text.ShouldBe(CustomizeCopy.CountText(2));
            AutomationProperties.GetLiveSetting(view.Count).ShouldBe(AutomationLiveSetting.Polite);
            view.Problems.IsVisible.ShouldBeTrue();
        }, ct).ConfigureAwait(true);
    }
}
