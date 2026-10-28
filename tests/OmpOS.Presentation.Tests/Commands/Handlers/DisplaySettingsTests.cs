using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Commands.Handlers;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Commands.Handlers;

/// <summary>Verifies the display switches: defaults, persistence in an OmpOS-owned file, damaged files, and the slash commands that flip them.</summary>
public sealed class DisplaySettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ompos-display-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string FilePath => Path.Combine(_folder, "config", "display.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static SlashActionRequest Typed(string typed) => new("x", string.Empty, typed);

    private sealed class FailingStore : IDisplaySettingsStore
    {
        public Task<DisplaySettingsValues> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(DisplaySettingsValues.Defaults);

        public Task SaveAsync(DisplaySettingsValues values, CancellationToken cancellationToken) => Task.FromException(new IOException("read-only disk"));
    }

    [Fact]
    public async Task WithoutAFileTheDefaultsApplyAndNothingIsWrittenByReading()
    {
        var settings = new DisplaySettings(new FileDisplaySettingsStore(FilePath));

        await settings.LoadAsync(Token).ConfigureAwait(true);

        settings.Values.ShouldBe(DisplaySettingsValues.Defaults);
        settings.VimMode.ShouldBeFalse();
        settings.RewindEnabled.ShouldBeTrue();
        File.Exists(FilePath).ShouldBeFalse();
    }

    [Fact]
    public async Task ASwitchIsSavedAndSurvivesANewModelAndOnlyTheChangedPropertyIsAnnounced()
    {
        var first = new DisplaySettings(new FileDisplaySettingsStore(FilePath));
        var names = PropertyChangeRecorder.Record(first);

        await first.SetAsync(DisplaySetting.VimMode, true, Token).ConfigureAwait(true);
        await first.SetAsync(DisplaySetting.ThinkingBlocks, false, Token).ConfigureAwait(true);
        await first.SetAsync(DisplaySetting.ThinkingBlocks, false, Token).ConfigureAwait(true);
        var second = new DisplaySettings(new FileDisplaySettingsStore(FilePath));
        await second.LoadAsync(Token).ConfigureAwait(true);

        second.VimMode.ShouldBeTrue();
        second.ShowThinkingBlocks.ShouldBeFalse();
        second.ShowLineNumbers.ShouldBeTrue();
        names.Where(name => !string.Equals(name, nameof(DisplaySettings.Values), StringComparison.Ordinal)).ShouldBe([nameof(DisplaySettings.VimMode), nameof(DisplaySettings.ShowThinkingBlocks)]);
        Directory.EnumerateFiles(Path.GetDirectoryName(FilePath)!).ShouldHaveSingleItem().ShouldBe(FilePath);
    }

    [Fact]
    public async Task ADamagedUnknownVersionOrExtraMemberFileReadsAsTheDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var store = new FileDisplaySettingsStore(FilePath);

        foreach (var text in new[] { "{ not json", "{\"version\":99,\"vimMode\":true}", "{\"version\":1,\"vimMode\":true,\"surprise\":1}", string.Empty })
        {
            await File.WriteAllTextAsync(FilePath, text, Token).ConfigureAwait(true);
            (await store.LoadAsync(Token).ConfigureAwait(true)).ShouldBe(DisplaySettingsValues.Defaults);
        }
    }

    [Theory]
    [InlineData(DisplaySetting.VimMode)]
    [InlineData(DisplaySetting.LineNumbers)]
    [InlineData(DisplaySetting.ThinkingBlocks)]
    [InlineData(DisplaySetting.StatusIndicators)]
    [InlineData(DisplaySetting.StatusLineRunningTime)]
    [InlineData(DisplaySetting.Rewind)]
    public void EachSwitchReadsAndWritesItsOwnValueAndNoOtherOne(DisplaySetting setting)
    {
        var flipped = DisplaySettingsValues.Defaults.With(setting, !DisplaySettingsValues.Defaults.Get(setting));

        foreach (var other in Enum.GetValues<DisplaySetting>())
        {
            flipped.Get(other).ShouldBe(other == setting ? !DisplaySettingsValues.Defaults.Get(other) : DisplaySettingsValues.Defaults.Get(other));
        }

        Should.Throw<ArgumentOutOfRangeException>(() => flipped.Get((DisplaySetting)99));
    }

    [Fact]
    public async Task TheCommandsTurnASwitchOnOffToggleItAndReportItsStatus()
    {
        var settings = new DisplaySettings(new FileDisplaySettingsStore(FilePath));
        var vim = new DisplayToggleSlashHandler(HandlerActionIds.DisplayVim, settings);

        (await vim.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true)).Message.ShouldBe("Vim keys in the composer: on.");
        settings.VimMode.ShouldBeTrue();
        (await vim.RunAsync(Typed("status"), Token).ConfigureAwait(true)).Message.ShouldBe("Vim keys in the composer: on.");
        (await vim.RunAsync(Typed("OFF"), Token).ConfigureAwait(true)).Message.ShouldBe("Vim keys in the composer: off.");
        (await vim.RunAsync(Typed("on"), Token).ConfigureAwait(true)).Outcome.ShouldBe(SlashActionOutcome.Completed);
        (await vim.RunAsync(Typed("toggle"), Token).ConfigureAwait(true)).Message.ShouldBe("Vim keys in the composer: off.");
        var bad = await vim.RunAsync(Typed("maybe"), Token).ConfigureAwait(true);

        bad.Outcome.ShouldBe(SlashActionOutcome.Failed);
        bad.Message.ShouldContain("type on, off, toggle or status", Case.Sensitive);
        (await new FileDisplaySettingsStore(FilePath).LoadAsync(Token).ConfigureAwait(true)).VimMode.ShouldBeFalse();
    }

    [Fact]
    public async Task EachDisplayCommandControlsItsOwnSwitch()
    {
        var settings = new DisplaySettings(new FileDisplaySettingsStore(FilePath));

        await new DisplayToggleSlashHandler(HandlerActionIds.DisplayLineNumbers, settings).RunAsync(Typed("off"), Token).ConfigureAwait(true);
        await new DisplayToggleSlashHandler(HandlerActionIds.DisplayShowThinking, settings).RunAsync(Typed("off"), Token).ConfigureAwait(true);
        await new DisplayToggleSlashHandler(HandlerActionIds.DisplayStatusIndicators, settings).RunAsync(Typed("off"), Token).ConfigureAwait(true);

        settings.Values.ShouldBe(new DisplaySettingsValues(false, false, false, false, true, true));
        Should.Throw<ArgumentException>(() => new DisplayToggleSlashHandler("display.other", settings));
    }

    [Fact]
    public async Task AStoreThatCannotSaveKeepsTheSwitchForThisRunAndTheResultSaysItWasNotSaved()
    {
        var settings = new DisplaySettings(new FailingStore());
        var handler = new DisplayToggleSlashHandler(HandlerActionIds.DisplayVim, settings);

        var result = await handler.RunAsync(Typed("on"), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SlashActionOutcome.Failed);
        result.Message.ShouldBe("Vim keys in the composer: on. It was not saved: read-only disk.");
        settings.VimMode.ShouldBeTrue();
    }
}
