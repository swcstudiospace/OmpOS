using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Native.Settings;
using Autonogrammer.Presentation.Settings.BrowserUse;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Browser;

/// <summary>Browser settings persist under <c>browserUse</c> and the head composition reads them back; disabled is the default.</summary>
public sealed class BrowserUseSettingsPersistenceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ag-browser-settings-").FullName;

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>An absent file gives the defaults: disabled, prompt on new origin, deny downloads, sandbox required.</summary>
    [Fact]
    public void AbsentFileGivesDisabledDefaults()
    {
        var values = new FileSettingsStore(_root).ReadBrowserUse();
        values.ShouldBe(BrowserUseSettingsValues.Defaults);
        values.Enabled.ShouldBeFalse();
    }

    /// <summary>Saved values survive a new store instance, including the enable switch and host lists.</summary>
    [Fact]
    public void SavedValuesRoundTripThroughANewStore()
    {
        var saved = new BrowserUseSettingsValues(
            "/usr/bin/chromium",
            BrowserUsePolicyMode.Open,
            ["example.com"],
            ["blocked.example"],
            BrowserUseDownloadMode.Quarantine,
            BrowserUseSandboxMode.Required,
            PersistentProfile: true,
            AllowDispatchSessions: false,
            LocalOriginsTrusted: true,
            Enabled: true);
        new FileSettingsStore(_root).SetBrowserUse(saved);

        new FileSettingsStore(_root).ReadBrowserUse().ShouldBe(saved);
    }

    /// <summary>A bad enum name in the file is a named settings error, never a silent default.</summary>
    [Fact]
    public void BadEnumInFileThrows()
    {
        var store = new FileSettingsStore(_root);
        store.SetBrowserUse(BrowserUseSettingsValues.Defaults);
        var text = File.ReadAllText(store.FilePath).Replace("PromptOnNewOrigin", "NotAPolicy", StringComparison.Ordinal);
        File.WriteAllText(store.FilePath, text);

        Should.Throw<Autonogrammer.Presentation.Settings.SettingsSourceException>(() => new FileSettingsStore(_root).ReadBrowserUse());
    }

    /// <summary>Applying persisted values to live options carries the enable switch; reading them back is the inverse.</summary>
    [Fact]
    public void ApplyAndReadAreInverse()
    {
        var values = BrowserUseSettingsValues.Defaults with { Enabled = true, AllowedHosts = ["a.example"] };
        var options = new BrowserUseOptions();
        BrowserUseSettingsPersistence.Apply(values, options);
        options.Enabled.ShouldBeTrue();
        BrowserUseSettingsPersistence.Read(options).ShouldBe(values);
    }
}
