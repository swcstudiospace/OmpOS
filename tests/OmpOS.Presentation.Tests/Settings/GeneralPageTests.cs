using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>The General page: load states, the motion draft, save, reset, discard and the privacy statement.</summary>
public sealed class GeneralPageTests
{
    /// <summary>The page shows Loading until the source answers, then Ready with the saved values.</summary>
    [Fact]
    public async Task LoadMovesFromLoadingToReady()
    {
        var source = new ScriptedGeneralSource();
        var page = new GeneralPage(source);
        page.State.ShouldBe(SettingsPageState.Loading);

        var pending = page.LoadAsync(TestContext.Current.CancellationToken);
        page.State.ShouldBe(SettingsPageState.Loading);

        source.CompleteLoad();
        await pending.ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.Motion.ShouldBe(MotionPreference.System);
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A load failure becomes the error state with the source's reason; nothing throws and nothing is editable or dirty.</summary>
    [Fact]
    public async Task ALoadFailureBecomesTheErrorState()
    {
        var source = new ScriptedGeneralSource(new GeneralSettingsValues(MotionPreference.Full, ThemePreference.Dark)) { LoadFailure = "preferences file is corrupt" };
        var page = new GeneralPage(source);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("preferences file is corrupt");
        page.SetMotion(MotionPreference.Reduced);
        page.Motion.ShouldBe(MotionPreference.System);
        page.IsDirty.ShouldBeFalse();
        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        source.SaveCalls.ShouldBe(0);

        source.LoadFailure = null;
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.LoadError.ShouldBeEmpty();
        page.Motion.ShouldBe(MotionPreference.Full);
    }

    /// <summary>Choosing a motion is a draft: dirty and announced, but nothing reaches the source until Save.</summary>
    [Fact]
    public async Task ChoosingMotionIsADraftUntilSaved()
    {
        var source = new ScriptedGeneralSource(GeneralSettingsValues.Defaults);
        var page = new GeneralPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var announced = PropertyChangeRecorder.Record(page);

        page.SetMotion(MotionPreference.Reduced);

        page.Motion.ShouldBe(MotionPreference.Reduced);
        page.SelectedMotionOption.Value.ShouldBe(MotionPreference.Reduced);
        page.IsDirty.ShouldBeTrue();
        announced.ShouldAnnounce(nameof(GeneralPage.Motion));
        announced.ShouldAnnounce(nameof(ISettingsPage.IsDirty));
        source.SaveCalls.ShouldBe(0);

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        source.SaveCalls.ShouldBe(1);
        source.Values.ShouldBe(new GeneralSettingsValues(MotionPreference.Reduced, ThemePreference.Light));
        page.Saved.Motion.ShouldBe(MotionPreference.Reduced);
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>The theme defaults to Light (OQ-94) and is offered as System, Light, Dark, in that order.</summary>
    [Fact]
    public async Task TheThemeDefaultsToLightAndIsOfferedInOrder()
    {
        var page = new GeneralPage(new ScriptedGeneralSource(GeneralSettingsValues.Defaults));
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Theme.ShouldBe(ThemePreference.Light);
        page.SelectedThemeOption.Value.ShouldBe(ThemePreference.Light);
        GeneralPage.ThemeOptions.Select(option => option.Value).ToArray()
            .ShouldBe([ThemePreference.System, ThemePreference.Light, ThemePreference.Dark]);
        GeneralPage.ThemeOptions.Select(option => option.Label).ToArray()
            .ShouldBe([SettingsCopy.ThemeSystemLabel, SettingsCopy.ThemeLightLabel, SettingsCopy.ThemeDarkLabel]);
        GeneralPage.ThemeOptions.ShouldAllBe(option => option.Label.Length > 0 && option.Description.Length > 0);
        GeneralSettingsValues.Defaults.Theme.ShouldBe(ThemePreference.Light);
    }

    /// <summary>Choosing a theme is a draft like motion: dirty and announced, nothing reaches the source until Save, and Save keeps the motion.</summary>
    [Fact]
    public async Task ChoosingAThemeIsADraftUntilSaved()
    {
        var source = new ScriptedGeneralSource(new GeneralSettingsValues(MotionPreference.Full, ThemePreference.Dark));
        var page = new GeneralPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var announced = PropertyChangeRecorder.Record(page);

        page.SetTheme(ThemePreference.Light);

        page.Theme.ShouldBe(ThemePreference.Light);
        page.SelectedThemeOption.Value.ShouldBe(ThemePreference.Light);
        page.IsDirty.ShouldBeTrue();
        announced.ShouldAnnounce(nameof(GeneralPage.Theme));
        announced.ShouldAnnounce(nameof(GeneralPage.SelectedThemeOption));
        announced.ShouldAnnounce(nameof(ISettingsPage.IsDirty));
        source.SaveCalls.ShouldBe(0);

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        source.SaveCalls.ShouldBe(1);
        source.Values.ShouldBe(new GeneralSettingsValues(MotionPreference.Full, ThemePreference.Light));
        page.Saved.Theme.ShouldBe(ThemePreference.Light);
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>Discard puts the saved theme back, and an undefined theme is refused.</summary>
    [Fact]
    public async Task DiscardRestoresTheSavedThemeAndAnUndefinedThemeIsRefused()
    {
        var page = new GeneralPage(new ScriptedGeneralSource(GeneralSettingsValues.Defaults));
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        page.SetTheme(ThemePreference.System);

        page.Discard();

        page.Theme.ShouldBe(ThemePreference.Light);
        page.IsDirty.ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => page.SetTheme((ThemePreference)42));
    }

    /// <summary>A rejected write keeps the draft and the old saved values, shows the reason inline, and a later save can succeed.</summary>
    [Fact]
    public async Task ARejectedWriteShowsAnInlineErrorAndCanBeRetried()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedGeneralSource(GeneralSettingsValues.Defaults) { SaveFailure = "preferences are read-only" };
        var page = new GeneralPage(source);
        await page.LoadAsync(token).ConfigureAwait(true);
        page.SetMotion(MotionPreference.Full);

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();

        page.HasError.ShouldBeTrue();
        page.SaveError.ShouldBe("preferences are read-only");
        page.IsDirty.ShouldBeTrue();
        page.Saved.Motion.ShouldBe(MotionPreference.System);

        source.SaveFailure = null;
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        page.HasError.ShouldBeFalse();
        page.SaveError.ShouldBeEmpty();
        page.IsDirty.ShouldBeFalse();
        source.Values.Motion.ShouldBe(MotionPreference.Full);
    }

    /// <summary>Reset returns the draft to the defaults without saving; discard returns it to the saved values and clears the error.</summary>
    [Fact]
    public async Task ResetAndDiscardMoveTheDraft()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedGeneralSource(new GeneralSettingsValues(MotionPreference.Full, ThemePreference.Dark)) { SaveFailure = "no" };
        var page = new GeneralPage(source);
        await page.LoadAsync(token).ConfigureAwait(true);

        page.Reset();

        page.Motion.ShouldBe(MotionPreference.System);
        page.Saved.Motion.ShouldBe(MotionPreference.Full);
        page.IsDirty.ShouldBeTrue();
        source.SaveCalls.ShouldBe(0);

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();
        page.HasError.ShouldBeTrue();

        page.Discard();

        page.Motion.ShouldBe(MotionPreference.Full);
        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();
    }

    /// <summary>An undefined motion is refused.</summary>
    [Fact]
    public async Task AnUndefinedMotionIsRefused()
    {
        var page = new GeneralPage(new ScriptedGeneralSource(GeneralSettingsValues.Defaults));
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        Should.Throw<ArgumentOutOfRangeException>(() => page.SetMotion((MotionPreference)42));
    }

    /// <summary>The page offers exactly the three motion choices and no telemetry control; the privacy statement says nothing is sent by OmpOS.</summary>
    [Fact]
    public void OffersMotionOnlyAndAStatementOfPrivacy()
    {
        GeneralPage.MotionOptions.Select(option => option.Value).ToArray()
            .ShouldBe([MotionPreference.System, MotionPreference.Reduced, MotionPreference.Full]);
        GeneralPage.MotionOptions.ShouldAllBe(option => option.Label.Length > 0 && option.Description.Length > 0);
        SettingsCopy.PrivacyStatement.ShouldContain("sends nothing anywhere itself", Case.Sensitive);
        SettingsCopy.PrivacyStatement.ShouldContain("omp", Case.Sensitive);
        typeof(GeneralPage).GetProperties().Select(property => property.Name)
            .ShouldNotContain(name => name.Contains("Telemetry", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A page without a source is refused.</summary>
    [Fact]
    public void ConstructionRefusesAMissingSource() =>
        Should.Throw<ArgumentNullException>(() => new GeneralPage(null!));
}
