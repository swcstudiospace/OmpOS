using System.Runtime.Versioning;
using System.Text.Json;
using Autonogrammer.Native.Settings;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>The head's settings file: its values, its modes, what it preserves and what it refuses to overwrite.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class FileSettingsStoreTests : IDisposable
{
    private readonly HeadTempDirectory _state = new();

    /// <inheritdoc />
    public void Dispose() => _state.Dispose();

    /// <summary>A state directory with no settings file reads as the defaults and creates nothing.</summary>
    [Fact]
    public void AMissingFileGivesTheDefaultsAndCreatesNothing()
    {
        var store = new FileSettingsStore(_state.PathOf("state"));

        store.Read().ShouldBe(StoredSettings.Defaults);

        File.Exists(store.FilePath).ShouldBeFalse();
        Directory.Exists(store.ConfigDirectory).ShouldBeFalse();
    }

    /// <summary>A file with no theme member reads as the Light default (OQ-94), a saved Dark is respected, and each theme survives a write, case-insensitively on read.</summary>
    [Fact]
    public void TheThemeDefaultsToLightAndRoundTrips()
    {
        StoredSettings.Defaults.Theme.ShouldBe(ThemePreference.Light);
        var store = new FileSettingsStore(_state.PathOf("theme"));
        store.Read().Theme.ShouldBe(ThemePreference.Light);

        foreach (var theme in new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark })
        {
            store.SetTheme(theme);

            store.Read().Theme.ShouldBe(theme);
        }

        _state.Write("theme2/config/settings.json", "{\"theme\":\"LIGHT\"}");
        new FileSettingsStore(_state.PathOf("theme2")).Read().Theme.ShouldBe(ThemePreference.Light);

        _state.Write("theme3/config/settings.json", "{\"motion\":\"full\",\"theme\":\"dark\"}");
        new FileSettingsStore(_state.PathOf("theme3")).Read().Theme.ShouldBe(ThemePreference.Dark);

        _state.Write("theme4/config/settings.json", "{\"motion\":\"full\"}");
        new FileSettingsStore(_state.PathOf("theme4")).Read().Theme.ShouldBe(ThemePreference.Light);
    }

    /// <summary>The file lives at config/settings.json below the state directory.</summary>
    [Fact]
    public void TheFileIsConfigSettingsJsonBelowTheStateDirectory()
    {
        var store = new FileSettingsStore(_state.PathOf("state"));

        store.FilePath.ShouldBe(_state.PathOf("state/config/settings.json"));
    }

    /// <summary>Every typed value survives a write and a read through a new store over the same directory.</summary>
    [Fact]
    public void EveryValueRoundTripsThroughANewStore()
    {
        var first = new FileSettingsStore(_state.Root);
        first.SetMotion(MotionPreference.Reduced);
        first.SetTheme(ThemePreference.Light);
        first.SetTerminalAutoRun(autoRun: true);
        first.SetRuntime(new RuntimeSettingsValues(12, 3));

        var read = new FileSettingsStore(_state.Root).Read();

        read.Motion.ShouldBe(MotionPreference.Reduced);
        read.Theme.ShouldBe(ThemePreference.Light);
        read.TerminalAutoRun.ShouldBeTrue();
        read.Runtime.ShouldBe(new RuntimeSettingsValues(12, 3));
    }

    /// <summary>Writing one value leaves the others as they were saved.</summary>
    [Fact]
    public void WritingOneValueKeepsTheOthers()
    {
        var store = new FileSettingsStore(_state.Root);
        store.SetRuntime(new RuntimeSettingsValues(5, 2));
        store.SetMotion(MotionPreference.Full);

        store.SetTerminalAutoRun(autoRun: true);

        var read = store.Read();
        read.Motion.ShouldBe(MotionPreference.Full);
        read.Runtime.ShouldBe(new RuntimeSettingsValues(5, 2));
        read.TerminalAutoRun.ShouldBeTrue();
    }

    /// <summary>The folder is owner-only (0700) and the file is owner-read-write (0600).</summary>
    [Fact]
    public void TheFolderIsOwnerOnlyAndTheFileIsOwnerReadWrite()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes do not exist on Windows.");
        var store = new FileSettingsStore(_state.Root);

        store.SetMotion(MotionPreference.Reduced);

        File.GetUnixFileMode(store.ConfigDirectory).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.GetUnixFileMode(store.FilePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>A folder left with looser modes is tightened when a write finds it.</summary>
    [Fact]
    public void AnExistingLooseFolderIsTightenedOnWrite()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes do not exist on Windows.");
        var store = new FileSettingsStore(_state.Root);
        Directory.CreateDirectory(store.ConfigDirectory);
        File.SetUnixFileMode(store.ConfigDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);

        store.SetMotion(MotionPreference.Full);

        File.GetUnixFileMode(store.ConfigDirectory).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>A write leaves no temporary file behind.</summary>
    [Fact]
    public void AWriteLeavesNoTemporaryFile()
    {
        var store = new FileSettingsStore(_state.Root);

        store.SetRuntime(new RuntimeSettingsValues(8, 8));
        store.SetMotion(MotionPreference.Reduced);

        Directory.GetFiles(store.ConfigDirectory).ShouldBe([store.FilePath]);
    }

    /// <summary>A file that is not JSON is an error state: reads and writes fail with a plain message and the file is not overwritten.</summary>
    [Fact]
    public void ACorruptFileIsAnErrorStateAndIsNeverOverwritten()
    {
        var store = new FileSettingsStore(_state.Root);
        var path = _state.Write("config/settings.json", "{ this is not json");

        var read = Should.Throw<SettingsSourceException>(() => store.Read());
        var write = Should.Throw<SettingsSourceException>(() => store.SetTerminalAutoRun(autoRun: true));

        read.Message.ShouldContain("is not valid", Case.Sensitive);
        read.Message.ShouldContain(path, Case.Sensitive);
        write.Message.ShouldContain("does not overwrite", Case.Sensitive);
        File.ReadAllText(path).ShouldBe("{ this is not json");
    }

    /// <summary>An empty file and a JSON null are error states too.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    public void AnEmptyOrNonObjectFileIsAnErrorState(string content)
    {
        var store = new FileSettingsStore(_state.Root);
        var path = _state.Write("config/settings.json", content);

        _ = Should.Throw<SettingsSourceException>(() => store.Read());
        _ = Should.Throw<SettingsSourceException>(() => store.SetMotion(MotionPreference.Full));

        File.ReadAllText(path).ShouldBe(content);
    }

    /// <summary>A file from a newer major schema is an error state that says which schema it is, and it is left untouched.</summary>
    [Fact]
    public void ANewerMajorSchemaIsAnErrorStateAndIsLeftUntouched()
    {
        const string Newer = "{\"schemaVersion\":2,\"motion\":\"full\",\"futureThing\":{\"a\":1}}";
        var store = new FileSettingsStore(_state.Root);
        var path = _state.Write("config/settings.json", Newer);

        var read = Should.Throw<SettingsSourceException>(() => store.Read());
        _ = Should.Throw<SettingsSourceException>(() => store.SetRuntime(new RuntimeSettingsValues(2, 2)));

        read.Message.ShouldContain("newer Autonogrammer", Case.Sensitive);
        read.Message.ShouldContain("schema 2", Case.Sensitive);
        File.ReadAllText(path).ShouldBe(Newer);
    }

    /// <summary>A value that cannot be used is an error state named after its member, and it is never repaired by overwriting.</summary>
    [Theory]
    [InlineData("{\"motion\":\"sideways\"}", "motion")]
    [InlineData("{\"theme\":\"sepia\"}", "theme")]
    [InlineData("{\"runtime\":{\"idleStopHours\":0}}", "runtime.idleStopHours")]
    [InlineData("{\"runtime\":{\"idleStopHours\":721}}", "runtime.idleStopHours")]
    [InlineData("{\"runtime\":{\"retentionDays\":91}}", "runtime.retentionDays")]
    public void AnUnusableValueIsAnErrorStateNamingTheMember(string content, string member)
    {
        var store = new FileSettingsStore(_state.Root);
        var path = _state.Write("config/settings.json", content);

        var read = Should.Throw<SettingsSourceException>(() => store.Read());
        _ = Should.Throw<SettingsSourceException>(() => store.SetTerminalAutoRun(autoRun: true));

        read.Message.ShouldContain($"'{member}'", Case.Sensitive);
        File.ReadAllText(path).ShouldBe(content);
    }

    /// <summary>A text value for auto-run is not the literal true: the file is unusable and auto-run never turns on from it.</summary>
    [Fact]
    public void ATextAutoRunNeverTurnsAutoRunOn()
    {
        var store = new FileSettingsStore(_state.Root);
        _ = _state.Write("config/settings.json", "{\"terminal\":{\"autoRun\":\"true\"}}");

        _ = Should.Throw<SettingsSourceException>(() => store.Read());
        AgentsSettingsSource.ToTerminalSettings(store).AutoRunEnabled.ShouldBeFalse();
    }

    /// <summary>Members this build does not know are written back unchanged, at the top level and inside the known objects.</summary>
    [Fact]
    public void UnknownMembersOfTheSameMajorSchemaAreKeptOnRewrite()
    {
        var store = new FileSettingsStore(_state.Root);
        _ = _state.Write(
            "config/settings.json",
            "{\"schemaVersion\":1,\"future\":{\"nested\":[1,2,3]},\"terminal\":{\"autoRun\":false,\"shellPath\":\"/bin/zsh\"},\"runtime\":{\"idleStopHours\":6,\"warnAfter\":4}}");

        store.SetTerminalAutoRun(autoRun: true);
        store.SetRuntime(new RuntimeSettingsValues(9, 4));

        using var document = JsonDocument.Parse(File.ReadAllText(store.FilePath));
        var root = document.RootElement;
        root.GetProperty("future").GetProperty("nested").GetArrayLength().ShouldBe(3);
        root.GetProperty("terminal").GetProperty("shellPath").GetString().ShouldBe("/bin/zsh");
        root.GetProperty("terminal").GetProperty("autoRun").GetBoolean().ShouldBeTrue();
        root.GetProperty("runtime").GetProperty("warnAfter").GetInt32().ShouldBe(4);
        root.GetProperty("runtime").GetProperty("idleStopHours").GetInt32().ShouldBe(9);
        root.GetProperty("schemaVersion").GetInt32().ShouldBe(FileSettingsStore.CurrentSchemaVersion);
    }

    /// <summary>The file holds only the known settings and no key material: its names and values are those of the three settings.</summary>
    [Fact]
    public void TheFileHoldsNoKeyMaterial()
    {
        var store = new FileSettingsStore(_state.Root);
        store.SetMotion(MotionPreference.Reduced);
        store.SetTerminalAutoRun(autoRun: true);
        store.SetRuntime(new RuntimeSettingsValues(3, 3));

        var text = File.ReadAllText(store.FilePath);

        foreach (var word in new[] { "key", "token", "secret", "password", "credential", "bearer" })
        {
            text.ShouldNotContain(word, Case.Insensitive);
        }
    }

    /// <summary>A runtime value outside the limits is refused before anything is written.</summary>
    [Fact]
    public void ARuntimeValueOutsideTheLimitsIsRefused()
    {
        var store = new FileSettingsStore(_state.Root);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => store.SetRuntime(new RuntimeSettingsValues(0, 5)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => store.SetRuntime(new RuntimeSettingsValues(5, 91)));

        File.Exists(store.FilePath).ShouldBeFalse();
    }

    /// <summary>A write raises Changed once; a refused write raises nothing.</summary>
    [Fact]
    public void ChangedIsRaisedByAWriteAndNotByARefusedWrite()
    {
        var store = new FileSettingsStore(_state.Root);
        var raised = 0;
        store.Changed += (_, _) => raised++;

        store.SetMotion(MotionPreference.Full);
        raised.ShouldBe(1);

        _ = _state.Write("config/settings.json", "garbage");
        _ = Should.Throw<SettingsSourceException>(() => store.SetMotion(MotionPreference.Reduced));
        raised.ShouldBe(1);
    }
}
