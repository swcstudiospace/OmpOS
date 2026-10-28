using OmpOS.Presentation.Cloud;
using OmpOS.Presentation.Commands.Handlers;
using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>The Display page (draft, save, reset, discard, failures), the About action of the General page and the Cloud page without a host.</summary>
public sealed class DisplaySettingsPageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(DisplaySettingsPage Page, Store Store, DisplaySettings Display)> OpenAsync(DisplaySettingsValues? stored = null)
    {
        var store = new Store { Values = stored ?? DisplaySettingsValues.Defaults };
        var display = new DisplaySettings(store);
        var page = new DisplaySettingsPage(display);
        await page.LoadAsync(Token).ConfigureAwait(true);
        return (page, store, display);
    }

    /// <summary>The page lists six switches in order, each named by its copy, and loads the stored values.</summary>
    [Fact]
    public async Task ThePageListsSixSwitchesAndLoadsTheStoredValues()
    {
        var (page, _, _) = await OpenAsync(DisplaySettingsValues.Defaults with { VimMode = true }).ConfigureAwait(true);

        DisplaySettingsPage.Toggles.Select(static toggle => toggle.Setting).ShouldBe(Enum.GetValues<DisplaySetting>());
        DisplaySettingsPage.Toggles.ShouldAllBe(static toggle => toggle.Label.Length > 0 && toggle.Help.Length > 0);
        page.State.ShouldBe(SettingsPageState.Ready);
        page.IsOn(DisplaySetting.VimMode).ShouldBeTrue();
        page.IsDirty.ShouldBeFalse();
        page.Section.ShouldBe(SettingsSection.Display);
    }

    /// <summary>A switch is a draft: nothing is written until Save, and Save writes only what changed through the shared settings.</summary>
    [Fact]
    public async Task ASwitchIsADraftUntilSave()
    {
        var (page, store, display) = await OpenAsync().ConfigureAwait(true);

        page.SetToggle(DisplaySetting.LineNumbers, false);

        page.IsDirty.ShouldBeTrue();
        store.Saves.ShouldBeEmpty();
        display.ShowLineNumbers.ShouldBeTrue();

        (await page.SaveAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        page.IsDirty.ShouldBeFalse();
        display.ShowLineNumbers.ShouldBeFalse();
        store.Saves.ShouldHaveSingleItem().ShowLineNumbers.ShouldBeFalse();
    }

    /// <summary>Discard returns to the saved values; Reset returns the draft to the defaults without saving.</summary>
    [Fact]
    public async Task DiscardAndResetChangeOnlyTheDraft()
    {
        var (page, store, _) = await OpenAsync(DisplaySettingsValues.Defaults with { VimMode = true, RewindEnabled = false }).ConfigureAwait(true);
        page.SetToggle(DisplaySetting.ThinkingBlocks, false);

        page.Discard();
        page.IsDirty.ShouldBeFalse();
        page.IsOn(DisplaySetting.ThinkingBlocks).ShouldBeTrue();

        page.Reset();
        page.IsOn(DisplaySetting.VimMode).ShouldBeFalse();
        page.IsOn(DisplaySetting.Rewind).ShouldBeTrue();
        page.IsDirty.ShouldBeTrue();
        store.Saves.ShouldBeEmpty();
    }

    /// <summary>A store that cannot be written is an inline save error and the page stays dirty; one that cannot be read puts the page in its error state.</summary>
    [Fact]
    public async Task FailuresAreInlineStates()
    {
        var (page, store, _) = await OpenAsync().ConfigureAwait(true);
        store.SaveFailure = new IOException("disk");
        page.SetToggle(DisplaySetting.VimMode, true);

        (await page.SaveAsync(Token).ConfigureAwait(true)).ShouldBeFalse();

        page.HasError.ShouldBeTrue();
        page.SaveError.ShouldBe(DisplayCopy.SaveFailed, Case.Sensitive);
        page.IsDirty.ShouldBeTrue();

        var broken = new DisplaySettingsPage(new DisplaySettings(new Store { LoadFailure = new UnauthorizedAccessException() }));
        await broken.LoadAsync(Token).ConfigureAwait(true);
        broken.State.ShouldBe(SettingsPageState.Error);
        broken.LoadError.ShouldBe(DisplayCopy.LoadFailed, Case.Sensitive);
    }

    /// <summary>The About button exists only when the head supplied an action, and pressing it runs the action.</summary>
    [Fact]
    public void TheGeneralPageOffersAboutOnlyWithAnAction()
    {
        var opened = 0;
        var with = new GeneralPage(new GeneralStore(), () => opened++);
        var without = new GeneralPage(new GeneralStore());

        with.CanOpenAbout.ShouldBeTrue();
        without.CanOpenAbout.ShouldBeFalse();
        with.OpenAbout();
        without.OpenAbout();
        opened.ShouldBe(1);
    }

    /// <summary>A Cloud page over the not-configured host shows the not-configured state and no agent, and the host never reports a setup.</summary>
    [Fact]
    public async Task TheCloudPageWithoutAHostIsNotConfigured()
    {
        using var model = new CloudAgentsViewModel(new NotConfiguredCloudHost(), TimeProvider.System);
        var page = new CloudSettingsPage(model);

        await page.LoadAsync(Token).ConfigureAwait(true);
        await model.LoadAsync(Token).ConfigureAwait(true);

        page.Section.ShouldBe(SettingsSection.Cloud);
        page.Title.ShouldBe(CloudCopy.Title, Case.Sensitive);
        page.IsDirty.ShouldBeFalse();
        model.State.ShouldBe(CloudAgentsViewState.NotConfigured);
        model.Rows.ShouldBeEmpty();
        model.CanCreate.ShouldBeFalse();
    }

    private sealed class Store : IDisplaySettingsStore
    {
        public DisplaySettingsValues Values { get; set; } = DisplaySettingsValues.Defaults;

        public Exception? LoadFailure { get; set; }

        public Exception? SaveFailure { get; set; }

        public List<DisplaySettingsValues> Saves { get; } = [];

        public Task<DisplaySettingsValues> LoadAsync(CancellationToken cancellationToken) =>
            LoadFailure is { } failure ? Task.FromException<DisplaySettingsValues>(failure) : Task.FromResult(Values);

        public Task SaveAsync(DisplaySettingsValues values, CancellationToken cancellationToken)
        {
            if (SaveFailure is { } failure)
            {
                return Task.FromException(failure);
            }

            Values = values;
            Saves.Add(values);
            return Task.CompletedTask;
        }
    }

    private sealed class GeneralStore : IGeneralSettingsSource
    {
        public Task<GeneralSettingsValues> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(GeneralSettingsValues.Defaults);

        public Task SaveAsync(GeneralSettingsValues values, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
