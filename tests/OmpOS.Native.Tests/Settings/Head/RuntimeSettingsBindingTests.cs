using OmpOS.Native.Settings;
using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>The Runtime page: its saved values persist through the settings file and take effect.</summary>
public sealed class RuntimeSettingsBindingTests : IDisposable
{
    private readonly HeadTempDirectory _state = new();

    /// <inheritdoc />
    public void Dispose() => _state.Dispose();

    /// <summary>The page starts over what the file holds.</summary>
    [Fact]
    public void ThePageStartsOverTheSavedValues()
    {
        var store = new FileSettingsStore(_state.Root);
        store.SetRuntime(new RuntimeSettingsValues(30, 11));

        var page = new RuntimeSettingsBinding(store, static _ => { }).CreatePage();

        page.Model.Saved.ShouldBe(new RuntimeSettingsValues(30, 11));
        page.Model.IdleStopText.ShouldBe("30");
        page.Model.RetentionText.ShouldBe("11");
    }

    /// <summary>A successful save is written to the file and put in force, once.</summary>
    [Fact]
    public async Task ASaveIsPersistedAndApplied()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileSettingsStore(_state.Root);
        var applied = new List<RuntimeSettingsValues>();
        var page = new RuntimeSettingsBinding(store, applied.Add).CreatePage();
        _ = page.Model.SetIdleStop("10");
        _ = page.Model.SetRetention("2");

        var saved = await page.SaveAsync(ct).ConfigureAwait(true);

        saved.ShouldBeTrue();
        store.Read().Runtime.ShouldBe(new RuntimeSettingsValues(10, 2));
        applied.ShouldBe([new RuntimeSettingsValues(10, 2)]);
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A saved value survives into the next window: a page built afterwards starts from it.</summary>
    [Fact]
    public async Task ASavedValueSurvivesIntoTheNextPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileSettingsStore(_state.Root);
        var binding = new RuntimeSettingsBinding(store, static _ => { });
        var first = binding.CreatePage();
        _ = first.Model.SetIdleStop("48");
        _ = await first.SaveAsync(ct).ConfigureAwait(true);

        var second = binding.CreatePage();

        second.Model.Saved.IdleStopHours.ShouldBe(48);
        second.Model.Saved.RetentionDays.ShouldBe(RuntimeSettingsLimits.DefaultRetentionDays);
    }

    /// <summary>A rejected entry cannot be saved: nothing is written and nothing is applied.</summary>
    [Fact]
    public async Task ARejectedEntryIsNeitherWrittenNorApplied()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileSettingsStore(_state.Root);
        var applied = new List<RuntimeSettingsValues>();
        var page = new RuntimeSettingsBinding(store, applied.Add).CreatePage();
        _ = page.Model.SetIdleStop("0");

        var saved = await page.SaveAsync(ct).ConfigureAwait(true);

        saved.ShouldBeFalse();
        applied.ShouldBeEmpty();
        File.Exists(store.FilePath).ShouldBeFalse();
    }

    /// <summary>When the file cannot be written the values still take effect for this run, and the failure is reported with its reason.</summary>
    [Fact]
    public async Task AFileThatCannotBeWrittenStillAppliesAndReportsWhy()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FileSettingsStore(_state.Root);
        var applied = new List<RuntimeSettingsValues>();
        var failures = new List<string>();
        var binding = new RuntimeSettingsBinding(store, applied.Add);
        binding.Failed += (_, failure) => failures.Add(failure.Failure.Message);
        var page = binding.CreatePage();
        var path = _state.Write("config/settings.json", "{ not json");
        _ = page.Model.SetRetention("5");

        var saved = await page.SaveAsync(ct).ConfigureAwait(true);

        saved.ShouldBeTrue();
        applied.ShouldBe([new RuntimeSettingsValues(RuntimeSettingsLimits.DefaultIdleStopHours, 5)]);
        failures.ShouldHaveSingleItem().ShouldContain("does not overwrite", Case.Sensitive);
        (await File.ReadAllTextAsync(path, ct).ConfigureAwait(true)).ShouldBe("{ not json");
    }

    /// <summary>A file that cannot be used starts the page from the defaults.</summary>
    [Fact]
    public void AnUnusableFileStartsThePageFromTheDefaults()
    {
        var store = new FileSettingsStore(_state.Root);
        _ = _state.Write("config/settings.json", "{ not json");

        var page = new RuntimeSettingsBinding(store, static _ => { }).CreatePage();

        page.Model.Saved.ShouldBe(RuntimeSettingsValues.Defaults);
    }
}
