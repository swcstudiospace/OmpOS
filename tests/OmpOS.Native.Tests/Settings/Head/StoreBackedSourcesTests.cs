using OmpOS.Native.Settings;
using OmpOS.Presentation;
using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>The General and Agents pages over the head's settings file, and the terminal settings the Terminal tool reads from it.</summary>
public sealed class StoreBackedSourcesTests : IDisposable
{
    private readonly HeadTempDirectory _state = new();

    /// <inheritdoc />
    public void Dispose() => _state.Dispose();

    /// <summary>The General page shows the saved motion and saving it writes it to the file.</summary>
    [Fact]
    public async Task TheGeneralPageReadsAndPersistsTheMotionChoice()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileSettingsStore(_state.Root);
        store.SetMotion(MotionPreference.Full);
        var page = new GeneralPage(new GeneralSettingsSource(store));

        await page.LoadAsync(ct).ConfigureAwait(true);
        page.Motion.ShouldBe(MotionPreference.Full);
        page.SetMotion(MotionPreference.Reduced);
        (await page.SaveAsync(ct).ConfigureAwait(true)).ShouldBeTrue();

        store.Read().Motion.ShouldBe(MotionPreference.Reduced);
    }

    /// <summary>The General page shows the saved theme, and saving a theme and a motion writes both in one change.</summary>
    [Fact]
    public async Task TheGeneralPageReadsAndPersistsTheThemeChoice()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileSettingsStore(_state.Root);
        store.SetTheme(ThemePreference.Light);
        var changes = 0;
        store.Changed += (_, _) => changes++;
        var page = new GeneralPage(new GeneralSettingsSource(store));

        await page.LoadAsync(ct).ConfigureAwait(true);
        page.Theme.ShouldBe(ThemePreference.Light);
        page.SetTheme(ThemePreference.System);
        page.SetMotion(MotionPreference.Full);
        (await page.SaveAsync(ct).ConfigureAwait(true)).ShouldBeTrue();

        var read = store.Read();
        read.Theme.ShouldBe(ThemePreference.System);
        read.Motion.ShouldBe(MotionPreference.Full);
        changes.ShouldBe(1, "the theme and the motion are one write, so the shell applies them once");
    }

    /// <summary>A settings file that cannot be used puts the General page in its error state with the plain message, and saving never overwrites it.</summary>
    [Fact]
    public async Task ACorruptFilePutsThePageInItsErrorState()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileSettingsStore(_state.Root);
        var path = _state.Write("config/settings.json", "{ nope");
        var page = new GeneralPage(new GeneralSettingsSource(store));

        await page.LoadAsync(ct).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldContain("is not valid", Case.Sensitive);
        (await File.ReadAllTextAsync(path, ct).ConfigureAwait(true)).ShouldBe("{ nope");
    }

    /// <summary>A write the file refuses is an inline save error on the page, not a thrown exception.</summary>
    [Fact]
    public async Task ARefusedWriteIsAnInlineSaveErrorOnThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileSettingsStore(_state.Root);
        var page = new AgentsPage(new AgentsSettingsSource(store));
        await page.LoadAsync(ct).ConfigureAwait(true);
        _ = _state.Write("config/settings.json", "{ nope");
        page.SetTerminalAutoRun(enabled: true);

        var saved = await page.SaveAsync(ct).ConfigureAwait(true);

        saved.ShouldBeFalse();
        page.HasError.ShouldBeTrue();
        page.IsDirty.ShouldBeTrue();
    }

    /// <summary>Auto-run is off until the Agents page saves it, and the Terminal tool's settings follow the saved value.</summary>
    [Fact]
    public async Task TheSavedAutoRunChoiceIsTheTerminalToolsSettings()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileSettingsStore(_state.Root);
        AgentsSettingsSource.ToTerminalSettings(store).AutoRunEnabled.ShouldBeFalse();
        var page = new AgentsPage(new AgentsSettingsSource(store));
        await page.LoadAsync(ct).ConfigureAwait(true);
        page.TerminalAutoRun.ShouldBeFalse();

        page.SetTerminalAutoRun(enabled: true);
        AgentsSettingsSource.ToTerminalSettings(store).AutoRunEnabled.ShouldBeFalse("an unsaved draft changes nothing");
        (await page.SaveAsync(ct).ConfigureAwait(true)).ShouldBeTrue();

        AgentsSettingsSource.ToTerminalSettings(store).AutoRunEnabled.ShouldBeTrue();
        page.SetTerminalAutoRun(enabled: false);
        (await page.SaveAsync(ct).ConfigureAwait(true)).ShouldBeTrue();
        AgentsSettingsSource.ToTerminalSettings(store).AutoRunEnabled.ShouldBeFalse();
    }

    /// <summary>A settings file that cannot be used never runs a command without an approval.</summary>
    [Fact]
    public void AnUnusableFileKeepsEveryCommandBehindAnApproval()
    {
        var store = new FileSettingsStore(_state.Root);
        store.SetTerminalAutoRun(autoRun: true);
        _ = _state.Write("config/settings.json", "{\"terminal\":{\"autoRun\":true},\"motion\":42}");

        AgentsSettingsSource.ToTerminalSettings(store).AutoRunEnabled.ShouldBeFalse();
    }
}
